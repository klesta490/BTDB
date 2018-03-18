# Changelog

## [unreleased]

## 12.6.0.0

### Fixed

Uninterupted influx of data after db opening can prevent compactor from running.

## 12.5.1.0

### Fixed

IPAddress can now serialize and deserialize null value.

## 12.5.0.0

### Added

*   Synchronization lock in EventLayer2 Deserialization to be on safe side.

## 12.4.0.0

### Added

*   PersistedNameAttribute is supported on Apart Fields in relation interfaces

## 12.3.0.0

### Added

*   IOC now resolves optional parameters that are not registered with its provided value

### Fixed

*   Fixed problem with calculating index from older version value in specific case

## 12.2.0.0

### Added

*   new method DeleteAllData() on ObjectDBTransaction
*   PersistedNameAttribute is additionally allowed on interfaces - useful for Relations

## 12.1.0.0

### Added

*   Event deserialization now automatically converts Enums to integer types.

## 12.0.0.0

### Added

*   Changelog
*   Nullable support in both ODb and EventStore