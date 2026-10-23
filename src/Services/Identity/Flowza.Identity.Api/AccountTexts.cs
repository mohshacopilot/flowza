namespace Flowza.Identity.Api;

/// <summary>
/// Texts of Identity's own pages (login, change password), from <c>Resources/AccountTexts.resx</c>
/// (constitution 12.1: no hard-coded screen text). English only in v0.1; another language is another .resx file.
/// </summary>
public sealed class AccountTexts
{
    public const string SkipToContent = "Layout.SkipToContent";

    /// <summary>The short line about the product on the brand panel (spec 007 C1: fixed, not editable in v0.1).</summary>
    public const string ProductLine = "Layout.ProductLine";

    /// <summary>The small product credit under the form (U3); the installation's own name is on the brand panel.</summary>
    public const string PoweredBy = "Layout.PoweredBy";

    // Spec 007 amendment 1, C8: the labels of the panel's workflow picture and the three highlights (fixed texts).
    public const string WorkflowForm = "Layout.Workflow.Form";
    public const string WorkflowApprove = "Layout.Workflow.Approve";
    public const string WorkflowDone = "Layout.Workflow.Done";
    public const string HighlightForms = "Layout.Highlight.Forms";
    public const string HighlightApprovals = "Layout.Highlight.Approvals";
    public const string HighlightTracking = "Layout.Highlight.Tracking";

    /// <summary>The highlights on the brand panel, in the order shown.</summary>
    public static IReadOnlyList<string> Highlights { get; } = [HighlightForms, HighlightApprovals, HighlightTracking];

    /// <summary>The show/hide button of every password field (UI-01-AC2); a toggle, so its name never changes.</summary>
    public const string ShowPassword = "Password.Show";

    public const string LoginTitle = "Login.Title";

    /// <summary>The short line under the login title (C8).</summary>
    public const string LoginIntro = "Login.Intro";
    public const string LoginUsername = "Login.Username";
    public const string LoginPassword = "Login.Password";
    public const string LoginSubmit = "Login.Submit";
    public const string LoginInvalid = "Login.Invalid";
    public const string LoginLockedOut = "Login.LockedOut";

    public const string ChangePasswordTitle = "ChangePassword.Title";
    public const string ChangePasswordIntro = "ChangePassword.Intro";
    public const string ChangePasswordCurrent = "ChangePassword.Current";
    public const string ChangePasswordNew = "ChangePassword.New";
    public const string ChangePasswordConfirm = "ChangePassword.Confirm";
    public const string ChangePasswordRules = "ChangePassword.Rules";
    public const string ChangePasswordSubmit = "ChangePassword.Submit";
    public const string ChangePasswordMismatch = "ChangePassword.Mismatch";

    public const string ErrorSummaryTitle = "Errors.SummaryTitle";

    /// <summary>Every key above, for the test that checks the resource file has them all.</summary>
    public static IReadOnlyList<string> AllKeys { get; } =
    [
        SkipToContent, ProductLine, PoweredBy, ShowPassword, WorkflowForm, WorkflowApprove, WorkflowDone,
        HighlightForms, HighlightApprovals, HighlightTracking, LoginTitle, LoginIntro, LoginUsername, LoginPassword, LoginSubmit, LoginInvalid, LoginLockedOut,
        ChangePasswordTitle, ChangePasswordIntro, ChangePasswordCurrent, ChangePasswordNew, ChangePasswordConfirm,
        ChangePasswordRules, ChangePasswordSubmit, ChangePasswordMismatch, ErrorSummaryTitle,
    ];

    /// <summary>Error codes these pages can show; they share the translation keys of the web app.</summary>
    public static IReadOnlyList<string> ErrorCodes { get; } =
    [
        "passwords.too_short", "passwords.common", "passwords.same_as_username", "passwords.current_wrong",
        "common.changed_by_someone_else",
    ];
}
