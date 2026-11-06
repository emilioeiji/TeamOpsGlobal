namespace TeamOps.Core.Security
{
    public static class PermissionActions
    {
        public const string View = "view";
        public const string Create = "create";
        public const string Edit = "edit";
        public const string Delete = "delete";
        public const string Export = "export";
        public const string Approve = "approve";
        public const string Administer = "administer";

        public static IReadOnlyList<string> All { get; } =
        [
            View, Create, Edit, Delete, Export, Approve, Administer
        ];
    }

    public static class ModuleCodes
    {
        public const string Dashboard = "dashboard";
        public const string Hikitsugui = "hikitsugui";
        public const string Haidai = "haidai";
        public const string Presence = "presence";
        public const string Production = "production";
        public const string SobraDePeca = "sobradepeca";
        public const string PrCl = "prcl";
        public const string FollowUp = "followup";
        public const string Tasks = "tasks";
        public const string MasterCard = "mastercard";
        public const string PaidLeave = "paidleave";
        public const string Reports = "reports";
        public const string Organization = "organization";
        public const string AccessControl = "accesscontrol";

        public static IReadOnlyList<string> All { get; } =
        [
            Dashboard, Hikitsugui, Haidai, Presence, Production, SobraDePeca,
            PrCl, FollowUp, Tasks, MasterCard, PaidLeave, Reports,
            Organization, AccessControl
        ];
    }
}
