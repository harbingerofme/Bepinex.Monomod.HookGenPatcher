# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [1.1.1] - 2026-5-2

### Other

- This is the same as 1.0.9

## [1.0.9] - 2025-10-27

### Other

- Uploaded to Thunderstore's Peglin Mod Database

## [1.0.8] - 2025-9-30

### Other

- Uploaded to Thunderstore's Hollow Knight: Silksong Mod Database

## [1.0.7] - 2025-6-19

### Other

- Uploaded to Thunderstore's PEAK Mod Database

## [1.0.6] - 2025-3-9

### Other

- Uploaded to the following Thunderstore Mod Databases:
  - Gloomwood
  - R.E.P.O.

## [1.0.5] - 2024-5-25

### Changed

- AutoHookGenPatcher is now also compatible with .NET Standard 2.0 (previously only 2.1)

## [1.0.4] - 2024-5-2

### Fixed

- Thunderstore description no longer has the second word also start with a capital letter

### Other

- Uploaded to Thunderstore's Lethal Company Mod Database

## [1.0.3] - 2024-4-12

### Fixed

- Handle error in case of BadImageFormatException when reading assemblies

## [1.0.2] - 2024-4-5

### Fixed

- Handle error if two non-plugin assemblies with the same name exist

## [1.0.1] - 2024-4-4

### Changed

- Thunderstore CLI is now used for building the Thunderstore package
- Improved README to explain better what AutoHookGenPatcher does

### Fixed

- Fixed a bug which prevented AutoHookGenPatcher from generating new version of MMHOOK assembly if that MMHOOK assembly existed already
- Minor typos in README

### Other

- Uploaded to Thunderstore's Plasma Mod Database

## [1.0.0] - 2024-4-2

### Changed

**Initial release — Differences to the original HookGenPatcher**

- Instead of only having a fixed list of files to generate MMHOOK files for, AutoHookGenPatcher will get the MMHOOK file references from installed plugins, and generates those MMHOOK files if possible.
- AutoHookGenPatcher makes use of a cache file to quickly check if everything is still up to date, without needing to check every MMHOOK file for that information.
- Hook Generation is now multithreaded, meaning that generating multiple MMHOOK files takes less time. For example, generating an MMHOOK file for every file in the `Managed` directory of Lethal Company takes about **22.5 seconds** instead of **40.0 seconds** it would take with no multithreading, on my machine.