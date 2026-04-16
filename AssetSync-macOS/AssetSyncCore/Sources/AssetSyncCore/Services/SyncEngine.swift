import Foundation
import os

/// Main sync orchestrator — coordinates the full sync cycle across
/// Intune, Iru, and Snipe-IT.
public actor SyncEngine: SyncEngineProtocol {
    private let intuneService: IntuneServiceProtocol
    private let iruService: IruServiceProtocol
    private let snipeItService: SnipeItServiceProtocol
    private let logRepository: LogRepositoryProtocol
    private let mappingRepository: MappingRepositoryProtocol
    private let configRepository: ConfigRepositoryProtocol
    private let webhookService: WebhookServiceProtocol
    private let connectivityTester: ConnectivityTesterProtocol
    private let merger: DeviceMerger
    private let resolver: ConflictResolver
    private let buildMapper: BuildVersionMapper
    private let logger = Logger(subsystem: "com.assetsync", category: "SyncEngine")

    /// Matches a standard PM asset tag: PM followed by exactly 7 digits.
    private static let pmTagRegex = /^PM\d{7}$/

    private static func isStandardTag(_ tag: String?) -> Bool {
        guard let tag, !tag.isEmpty else { return false }
        return tag.wholeMatch(of: pmTagRegex.ignoresCase()) != nil
    }

    public init(
        intuneService: IntuneServiceProtocol,
        iruService: IruServiceProtocol,
        snipeItService: SnipeItServiceProtocol,
        logRepository: LogRepositoryProtocol,
        mappingRepository: MappingRepositoryProtocol,
        configRepository: ConfigRepositoryProtocol,
        webhookService: WebhookServiceProtocol,
        connectivityTester: ConnectivityTesterProtocol,
        merger: DeviceMerger,
        resolver: ConflictResolver,
        buildMapper: BuildVersionMapper
    ) {
        self.intuneService = intuneService
        self.iruService = iruService
        self.snipeItService = snipeItService
        self.logRepository = logRepository
        self.mappingRepository = mappingRepository
        self.configRepository = configRepository
        self.webhookService = webhookService
        self.connectivityTester = connectivityTester
        self.merger = merger
        self.resolver = resolver
        self.buildMapper = buildMapper
    }

    public func runSync(dryRun: Bool) async throws -> SyncRunSummary {
        let runId = UUID().uuidString
        var summary = SyncRunSummary()
        summary.syncRunId = runId
        summary.startedAtUtc = Date()
        summary.dryRun = dryRun

        await log(runId: runId, level: .info, source: .application,
                  action: dryRun ? "sync_start_dry" : "sync_start")

        do {
            // 1. Connectivity checks
            let snipeStatus = await connectivityTester.testSnipeIt()
            summary.snipeItReachable = snipeStatus.state == .connected
            guard summary.snipeItReachable else {
                await webhookService.sendConnectivityFailureNotification(
                    serviceName: "Snipe-IT", message: snipeStatus.message ?? "Unreachable")
                summary.completedAtUtc = Date()
                await log(runId: runId, level: .error, source: .application,
                          action: "sync_complete", errorDetail: "Snipe-IT unreachable")
                await webhookService.sendSyncNotification(summary)
                return summary
            }

            let intuneStatus = await connectivityTester.testIntune()
            summary.intuneReachable = intuneStatus.state == .connected
            let iruStatus = await connectivityTester.testIru()
            summary.iruReachable = iruStatus.state == .connected

            guard summary.intuneReachable || summary.iruReachable else {
                await webhookService.sendConnectivityFailureNotification(
                    serviceName: "Intune and Iru", message: "Both MDM sources unreachable")
                summary.completedAtUtc = Date()
                await log(runId: runId, level: .error, source: .application,
                          action: "sync_complete", errorDetail: "No MDM source available")
                await webhookService.sendSyncNotification(summary)
                return summary
            }

            // 2. Fetch devices
            let intuneList = summary.intuneReachable ? (try? await intuneService.getManagedDevices()) ?? [] : []
            let iruList = summary.iruReachable ? (try? await iruService.getDevices()) ?? [] : []

            let writeBackIntune = (try? await configRepository.getWriteBackIntuneEnabled()) ?? false
            let writeBackIru = (try? await configRepository.getWriteBackIruEnabled()) ?? false
            let intuneMdmWins = (try? await configRepository.getIntuneMdmWins()) ?? false
            let iruMdmWins = (try? await configRepository.getIruMdmWins()) ?? false

            let ignoredModels = Set((try? await mappingRepository.getIgnoredModels()) ?? [])

            // 3. Merge
            let merged = merger.merge(intuneDevices: intuneList, iruDevices: iruList)

            // 4. Process each device
            for var device in merged {
                if device.normalizedSerial.isEmpty {
                    device.normalizedSerial = SerialNumberNormalizer.normalize(device.serialNumber)
                }

                let existing = (try? await snipeItService.searchAssetsBySerial(device.normalizedSerial)) ?? []

                if existing.count > 1 {
                    summary.errors += 1
                    await log(runId: runId, level: .error, source: .snipeIt, action: "error",
                              serial: device.serialNumber, deviceName: device.deviceName,
                              success: false, errorDetail: "Multiple Snipe-IT assets with same serial")
                    continue
                }

                if existing.isEmpty {
                    // Create flow
                    guard let modelMapping = try? await mappingRepository.getModelMapping(
                        mdmModelString: device.model ?? "") else {
                        summary.skipped += 1
                        if !ignoredModels.contains(device.model?.lowercased() ?? "") {
                            await log(runId: runId, level: .warning, source: .application, action: "skip",
                                      serial: device.serialNumber, deviceName: device.deviceName,
                                      errorDetail: "Pending model mapping: \(device.model ?? "")")
                        }
                        continue
                    }
                    device.snipeItModelId = modelMapping.snipeItModelId
                    device.windowsFeatureUpdate = await buildMapper.getFriendlyName(for: device.osVersion)
                    if let catMapping = try? await mappingRepository.getCategoryMapping(
                        mdmDeviceType: device.deviceType ?? "") {
                        device.snipeItCategoryId = catMapping.snipeItCategoryId
                    }
                    if Self.isStandardTag(device.mdmAssetTag) {
                        device.mdmAssetTag = device.mdmAssetTag?.uppercased()
                    }
                    if !dryRun {
                        do {
                            if let created = try await snipeItService.createAsset(device) {
                                summary.created += 1
                                await log(runId: runId, level: .info, source: .snipeIt, action: "create",
                                          serial: device.serialNumber, deviceName: device.deviceName)
                                await writeBackAssetTag(runId: runId, device: device,
                                                        assetTag: created.snipeItAssetTag,
                                                        writeBackIntune: writeBackIntune,
                                                        writeBackIru: writeBackIru)
                            } else {
                                summary.errors += 1
                                await log(runId: runId, level: .error, source: .snipeIt, action: "error",
                                          serial: device.serialNumber, deviceName: device.deviceName,
                                          success: false, errorDetail: "Create failed")
                            }
                        } catch {
                            summary.errors += 1
                            await log(runId: runId, level: .error, source: .snipeIt, action: "error",
                                      serial: device.serialNumber, deviceName: device.deviceName,
                                      success: false, errorDetail: error.localizedDescription)
                        }
                    } else {
                        summary.created += 1
                        await log(runId: runId, level: .info, source: .application, action: "create",
                                  serial: device.serialNumber, deviceName: device.deviceName,
                                  errorDetail: "[DRY RUN]")
                    }
                    continue
                }

                // Update flow
                let snipeAsset = existing[0]
                let mdmWins: Bool
                if device.platformSource == "Intune" { mdmWins = intuneMdmWins }
                else if device.platformSource == "Iru" { mdmWins = iruMdmWins }
                else { mdmWins = device.iruDeviceId?.isEmpty == false ? iruMdmWins : intuneMdmWins }

                let resolvedUpdates = resolver.getUpdatesToApply(
                    snipeItAsset: snipeAsset, mdmDevice: device, mdmWins: mdmWins)
                let discrepancies = resolver.getDiscrepancies(
                    snipeItAsset: snipeAsset, mdmDevice: device)
                for d in discrepancies {
                    await log(runId: runId, level: .warning, source: .application, action: "skip",
                              serial: device.serialNumber, deviceName: device.deviceName,
                              errorDetail: "Discrepancy \(d.field): Snipe-IT=\(d.snipeItValue ?? "") MDM=\(d.mdmValue ?? "")")
                }

                var updates = resolvedUpdates
                if Self.isStandardTag(device.mdmAssetTag) && !Self.isStandardTag(snipeAsset.snipeItAssetTag) {
                    updates["asset_tag"] = device.mdmAssetTag!.uppercased()
                    await log(runId: runId, level: .info, source: .application, action: "asset_tag_push",
                              serial: device.serialNumber, deviceName: device.deviceName,
                              errorDetail: "MDM asset tag '\(device.mdmAssetTag ?? "")' → Snipe-IT (was: '\(snipeAsset.snipeItAssetTag ?? "none")')")
                }

                guard !updates.isEmpty else {
                    summary.skipped += 1
                    continue
                }

                if !dryRun, let assetId = snipeAsset.snipeItAssetId {
                    let ok = (try? await snipeItService.updateAsset(assetId: assetId, updates: updates)) ?? false
                    if ok {
                        summary.updated += 1
                        await log(runId: runId, level: .info, source: .snipeIt, action: "update",
                                  serial: device.serialNumber, deviceName: device.deviceName)
                        await writeBackAssetTag(runId: runId, device: device,
                                                assetTag: snipeAsset.snipeItAssetTag,
                                                writeBackIntune: writeBackIntune,
                                                writeBackIru: writeBackIru)
                    } else {
                        summary.errors += 1
                        await log(runId: runId, level: .error, source: .snipeIt, action: "error",
                                  serial: device.serialNumber, deviceName: device.deviceName,
                                  success: false, errorDetail: "Update failed")
                    }
                } else {
                    summary.updated += 1
                    await log(runId: runId, level: .info, source: .application, action: "update",
                              serial: device.serialNumber, deviceName: device.deviceName,
                              errorDetail: dryRun ? "[DRY RUN]" : nil)
                }
            }

            summary.completedAtUtc = Date()
            await log(runId: runId, level: .info, source: .application, action: "sync_complete")
            await webhookService.sendSyncNotification(summary)

            try? await logRepository.purgeOlderThan(30 * 24 * 60 * 60) // 30 days
        } catch {
            logger.error("Sync failed: \(error.localizedDescription)")
            summary.completedAtUtc = Date()
            summary.errors += 1
            await log(runId: runId, level: .error, source: .application, action: "sync_complete",
                      success: false, errorDetail: "\(error.localizedDescription)\n\(String(describing: error))")
            await webhookService.sendSyncNotification(summary)
        }

        return summary
    }

    // MARK: - Private helpers

    private func writeBackAssetTag(runId: String, device: Device, assetTag: String?,
                                    writeBackIntune: Bool, writeBackIru: Bool) async {
        guard let assetTag, !assetTag.isEmpty,
              assetTag.wholeMatch(of: Self.pmTagRegex.ignoresCase()) != nil else { return }

        if writeBackIntune, let intuneId = device.intuneDeviceId, !intuneId.isEmpty,
           device.platformSource == "Intune" {
            let alreadyInNotes = device.intuneNotes?.localizedCaseInsensitiveContains("Asset Tag: \(assetTag)") ?? false
            if !alreadyInNotes {
                do {
                    _ = try await intuneService.writeBackAssetTag(
                        intuneDeviceId: intuneId, assetTag: assetTag, existingNotes: device.intuneNotes)
                    await log(runId: runId, level: .info, source: .intune, action: "write_back",
                              serial: device.serialNumber, deviceName: device.deviceName,
                              errorDetail: "Asset tag '\(assetTag)' written to Intune notes")
                } catch {
                    await log(runId: runId, level: .warning, source: .intune, action: "write_back",
                              serial: device.serialNumber, deviceName: device.deviceName,
                              success: false, errorDetail: error.localizedDescription)
                }
            }
        }

        if writeBackIru, let iruId = device.iruDeviceId, !iruId.isEmpty,
           device.platformSource == "Iru" {
            guard !assetTag.caseInsensitiveCompare(device.mdmAssetTag ?? "").rawValue.signum() == 0 else { return }
            let ok = (try? await iruService.writeBackAssetTag(iruDeviceId: iruId, assetTag: assetTag)) ?? false
            await log(runId: runId, level: ok ? .info : .warning, source: .iru, action: "write_back",
                      serial: device.serialNumber, deviceName: device.deviceName,
                      success: ok, errorDetail: ok ? "Asset tag '\(assetTag)' written to Iru" : "Write-back to Iru failed")
        }
    }

    private func log(runId: String, level: LogLevel, source: SourceSystem, action: String,
                     serial: String? = nil, deviceName: String? = nil,
                     success: Bool = true, errorDetail: String? = nil) async {
        var entry = LogEntry()
        entry.timestampUtc = Date()
        entry.level = level
        entry.sourceSystem = source
        entry.action = action
        entry.serialNumber = serial
        entry.deviceName = deviceName
        entry.success = success
        entry.errorDetail = errorDetail
        entry.syncRunId = runId
        try? await logRepository.append(entry)
    }
}
