# Accounts

Identity forms use static SSR through the module-level ExcludeFromInteractiveRouting attribute. Cookies are written during HTTP form submissions; forms include antiforgery tokens. Blazor Interactive Server is used for the rest of the application.

Registration delegates to RegisterAccount in the Register slice. It creates a new DI scope so UserManager and ApplicationDbContext share one transaction without reusing a circuit context. Registration must create or join a household; external account creation is disabled.

Login supports email/password, remembered sessions, lockout after repeated failures, and optional authenticator-based two-factor authentication. Successful login defaults to /Dashboard. Household access is revalidated independently of the Identity cookie.

Basic account settings include the template profile settings and password changes. Personal-data download validates antiforgery explicitly. DeleteAccount requires no household membership and a correct password; the persistence foreign key also prevents concurrent joining from being bypassed through deletion. Owners must transfer ownership and leave before deleting.

Email delivery is not configured and email confirmation is not required. Forgot-password and email-change pages explicitly explain that those operations are unavailable. RegisterConfirmation never exposes a generated confirmation token. No real email delivery is implied.

Identity HTTP endpoints sit beside their corresponding use cases: Logout, Login (passkey request adapter), ExternalLogin, Manage/Passkeys, Manage/ExternalLogins, and Manage/PersonalData. The module-level endpoint extension only composes their routes. External sign-in and passkeys are not offered by the initial UI; passkey adapters require HTTPS for future use.

Shared Identity helpers and module UI live in Shared or at module level. The account settings layout remains usable for authenticated accounts without household access, including removed members.

Verification includes registration and rollback, real SSR posts and cookies, login/logout/password changes, personal-data antiforgery, account deletion guards, and household access after revocation. See [root README](../../../../README.md).
