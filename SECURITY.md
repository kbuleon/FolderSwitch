# Security policy

## Supported versions

Only the latest release of FolderSwitch receives security fixes. The version is shown in the title bar and in the About
window: update before reporting.

## Reporting a vulnerability

**Do not open a public issue.** Report the vulnerability privately, with the « Report a vulnerability » button of the
repository's Security tab, or directly at <https://github.com/kbuleon/FolderSwitch/security/advisories/new>. Only the
author and you can read the report.

Please include:

- the FolderSwitch version and the Windows version;
- what an attacker can do, and under which conditions;
- the steps to reproduce it, or a proof of concept.

## What to expect

FolderSwitch is a personal project, maintained in free time by a single author: reports are answered as soon as
possible, without a guaranteed delay. The discussion stays private until a fixed version is released; the advisory is
then published, crediting the reporter unless they prefer otherwise.

## Scope

The parts of FolderSwitch where a vulnerability matters most:

- the update: the check of the feed, the download and the installation of a new version;
- the favicons of web sites: the pages and images fetched from the network, and their decoding;
- the low-level keyboard hook behind the global shortcut;
- the opening of folders, files and web sites, and the check of their addresses;
- the reading of the configuration file, and of a configuration imported from a file.

A bug without security consequence is reported in a public issue: see [CONTRIBUTING.md](CONTRIBUTING.md).
