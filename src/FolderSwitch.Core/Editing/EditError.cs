// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Karl BULEON
// Additional terms under GPLv3 section 7: see the License section of README.md.

namespace FolderSwitch.Core.Editing;

public enum EditError
{
    None,
    NotFound,
    EmptyName,
    LastTabCannotBeRemoved,
    ColumnNotEmpty,
    LastColumnCannotBeRemoved,
    InvalidIcon,
}
