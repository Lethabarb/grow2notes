namespace Grow2Notes.Web.Platform.Audit;

/// <summary>
/// The <c>AuditEvent.EventType</c> of each event in design.md §5.4's catalogue (A28), spelt as there and grouped by the
/// part before the dot, in the catalogue's order. A test reads the catalogue from design.md and fails unless these are
/// exactly its events, so an event added to the design needs its constant here, and a constant here needs its event in
/// the design.
/// </summary>
internal static class AuditEventTypes
{
    public static class Auth
    {
        public const string SignInSucceeded = "auth.signin_succeeded";

        /// <summary>
        /// For a known account only: a failure for an email with no account goes to telemetry as a count, with no
        /// email.
        /// </summary>
        public const string SignInFailed = "auth.signin_failed";

        public const string MfaFailed = "auth.mfa_failed";

        public const string LockedOut = "auth.locked_out";

        public const string SignedOut = "auth.signed_out";

        public const string SetupCompleted = "auth.setup_completed";
    }

    public static class User
    {
        public const string Invited = "user.invited";

        /// <summary>A change of name, email or role.</summary>
        public const string Updated = "user.updated";

        /// <summary>An invite resent or a sign-in reset.</summary>
        public const string SetupLinkSent = "user.setup_link_sent";

        public const string Deactivated = "user.deactivated";

        public const string Reactivated = "user.reactivated";
    }

    public static class Participant
    {
        public const string Created = "participant.created";

        public const string Updated = "participant.updated";

        public const string Archived = "participant.archived";

        public const string Restored = "participant.restored";

        /// <summary>A participant record export, which the catalogue lists with the report downloads.</summary>
        public const string Exported = "participant.exported";
    }

    public static class Goal
    {
        public const string Created = "goal.created";

        public const string Updated = "goal.updated";

        public const string Archived = "goal.archived";

        public const string Restored = "goal.restored";

        public const string Reordered = "goal.reordered";
    }

    public static class CommonItem
    {
        public const string Created = "common_item.created";

        /// <summary>A change to the item, a move to another group included.</summary>
        public const string Updated = "common_item.updated";

        public const string Archived = "common_item.archived";

        public const string Restored = "common_item.restored";

        public const string Reordered = "common_item.reordered";
    }

    public static class CommonItemGroup
    {
        public const string Created = "common_item_group.created";

        public const string Updated = "common_item_group.updated";

        public const string Archived = "common_item_group.archived";

        public const string Restored = "common_item_group.restored";

        public const string Reordered = "common_item_group.reordered";
    }

    public static class GuidePrompts
    {
        public const string Updated = "guide_prompts.updated";
    }

    public static class Note
    {
        public const string DraftStarted = "note.draft_started";

        public const string Discarded = "note.discarded";

        /// <summary>The first submit, which makes version 1.</summary>
        public const string Submitted = "note.submitted";

        /// <summary>Save changes on a submitted note, which makes version 2 or later.</summary>
        public const string Edited = "note.edited";

        public const string Reviewed = "note.reviewed";
    }

    public static class Report
    {
        public const string Downloaded = "report.downloaded";
    }

    /// <summary>The operator commands (design.md §7.4).</summary>
    public static class Admin
    {
        public const string Bootstrap = "admin.bootstrap";

        public const string SignInReset = "admin.signin_reset";

        public const string SignOutAll = "admin.signout_all";
    }
}
