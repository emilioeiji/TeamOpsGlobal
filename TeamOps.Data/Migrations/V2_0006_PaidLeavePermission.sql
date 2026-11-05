INSERT OR IGNORE INTO ModulePermissions (ProfileId, ModuleCode, ActionCode, IsAllowed)
SELECT profile.Id, 'paidleave', action.ActionCode, 1
FROM AccessProfiles profile
CROSS JOIN (
    SELECT 'view' AS ActionCode UNION ALL
    SELECT 'create' UNION ALL
    SELECT 'edit' UNION ALL
    SELECT 'export'
) action
WHERE profile.HierarchyRank >= 1;

INSERT OR IGNORE INTO ModulePermissions (ProfileId, ModuleCode, ActionCode, IsAllowed)
SELECT profile.Id, 'paidleave', action.ActionCode, 1
FROM AccessProfiles profile
CROSS JOIN (
    SELECT 'delete' AS ActionCode UNION ALL
    SELECT 'approve' UNION ALL
    SELECT 'administer'
) action
WHERE profile.HierarchyRank >= 3;
