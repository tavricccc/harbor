# Gopeed Native
<!-- impeccable:product-schema 1 -->

## Platform
Windows desktop, WinUI 3.

## Stack
User-confirmed WinUI 3 interface; Gopeed 1.9.3 Go download core. C# and MVVM for the frontend, independent Go process for the core. GPL-3.0 distribution, retaining upstream notices.

## Users and purpose
The user wants a practical Windows download manager with Gopeed's download capabilities and native Windows design and operations. RAM use matters. Traditional Chinese interface. The user explicitly delegates completion and design decisions; no original button layout must be preserved. After the Windows certificate-trust elevation was cancelled, deployment uses a per-user self-contained installer and portable build.

## Capabilities and constraints
Manage download creation, progress, pause/resume, retry, deletion, files and configuration. Retain the upstream engine and extension system. Build, run, verify real downloads and package locally. Separate UI lifetime from ongoing transfers. No Flutter or web UI in the application.

## Product principles
Native controls, visible progress and recoverable errors. Separate engine, API models and UI. Commit in batches. Report measured memory, not estimated savings.

## Visual commitment
Use the user's own Downlism native application as the implementation reference across the download list, confirmation, progress, details and settings. Match its structure, spacing, typography and native window treatment. Keep Gopeed-specific functionality in that same system. Extension UI is excluded from this redesign; retain official browser extension integration.

## Assumptions delegated to implementation
Product name Harbor. Identity uses a dock and incoming file mark in petrol and seafoam; native controls continue following Downlism. Target Windows 10 1809 and Windows 11, x64 first. Scope is an independent frontend rather than changes to other installed download managers. Core updates are pinned and explicit.
