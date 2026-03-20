# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Repository

**Type:** Public (GitHub)
**Remote:** `https://github.com/NickGraham101/gandt-infrastructure-components`

**Branch naming:** UpperCamelCase with no separators (e.g. `MyFeatureBranch`).

**Worktrees:** Create in `../gandt-infrastructure-components-worktrees/<branch-name>/` — never inside this directory.

## What This Project Is

A **Pulumi component plugin** written in C# that provides reusable infrastructure components. Currently contains one component: `IngressDns`, which creates AWS Route53 DNS A records pointing to an Azure Kubernetes Service ingress controller's public IP address.

The plugin is designed to be consumed as a local Pulumi package by a consumer stack — changes require rebuilding before running `pulumi up` in the consuming project.

## Build

```bash
dotnet build
```

The solution (`gandt-infrastructure-components.sln`) currently includes only the main component project. Test projects are referenced separately.

## Architecture

### Component: `ingress-dns/`

`IngressDns.cs` defines the `IngressDns` class (a `ComponentResource`) and its `IngressDnsArgs`. The component:

1. Looks up an AWS Route53 hosted zone by name
2. Looks up an Azure public IP resource by name and resource group
3. Creates a Route53 A record (`primaryRecord`) pointing to that IP
4. Optionally creates a root domain A record (when `CreateRootRecord` is true)

Optional `PrimaryRecordImportId` / `RootRecordImportId` and `PrimaryRecordAlias` / `RootRecordAlias` inputs support Pulumi state import and resource aliasing.

**Output:** `primaryRecord` — the FQDN of the primary DNS record.

`Program.cs` is the Pulumi plugin host entry point (`ComponentProviderHost.Serve()`).

`PulumiPlugin.yaml` declares the component metadata: `name: ingress-dns`, `runtime: dotnet`.

