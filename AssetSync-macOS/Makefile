.PHONY: generate build test archive dmg clean

SCHEME = AssetSync
CONFIG = Release

# Generate .xcodeproj from project.yml
generate:
	xcodegen generate

# Build for arm64 (Apple Silicon only)
build: generate
	xcodebuild build \
		-project AssetSync.xcodeproj \
		-scheme $(SCHEME) \
		-configuration Debug \
		ARCHS=arm64

# Run unit tests
test: generate
	xcodebuild test \
		-project AssetSync.xcodeproj \
		-scheme $(SCHEME) \
		-configuration Debug \
		ARCHS=arm64

# Archive a release build
archive: generate
	xcodebuild archive \
		-project AssetSync.xcodeproj \
		-scheme $(SCHEME) \
		-configuration $(CONFIG) \
		-archivePath build/AssetSync.xcarchive \
		ARCHS=arm64

# Create signed DMG
dmg:
	./Scripts/create-dmg.sh

clean:
	rm -rf build/
	xcodebuild clean -project AssetSync.xcodeproj -scheme $(SCHEME) 2>/dev/null || true
