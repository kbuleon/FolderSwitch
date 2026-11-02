// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Karl BULEON
// Additional terms under GPLv3 section 7: see the License section of README.md.

// The debug log is one static object of the application: its tests open, roll and close it one after the other, and
// nothing else may write to it meanwhile.
[assembly: CollectionBehavior(DisableTestParallelization = true)]
