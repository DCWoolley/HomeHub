# Account feature

This feature is the Web adapter for ASP.NET Core Identity. Public account pages live in use-case folders such as `Login` and `Register`; authenticated account settings live in `Manage/<UseCase>`. HTTP endpoints live beside their related pages. `AccountEndpoints` maps them under the existing `/Account` and `/Account/Manage` routes. Shared account components live in `Shared`.

## Boundaries

- Web owns static server-rendered forms, HTTP cookies, antiforgery handling, navigation, and circuit authentication-state revalidation. `AddAccountFeature` registers Web-specific services.
- Infrastructure owns `Identity/ApplicationUser`, `Identity/IdentityNoOpEmailSender`, and `Persistence/ApplicationDbContext`, including its migrations. `AddInfrastructure` registers the SQL Server store and Identity managers, token providers, and options.
- The account adapter uses Identity managers and Infrastructure's Identity user type directly. It must not query the database context. Domain and Application remain independent of authentication frameworks. Business features that need user capabilities should define focused Application contracts, implemented in Infrastructure.

## Behavior to preserve

- Account management nests `ManageLayout` inside the shared `AppLayout` sidebar. `ManageNavMenu` supplies its account submenu, including conditional external-login navigation. Public account pages stay sidebar-free for all visitors. The sidebar account menu signs out through the existing POST endpoint with an antiforgery token.

- All public account pages, authenticated account settings, and home management use `Components/Shared/AccountCard.razor` and its scoped styles to match the home page. Account entry pages retain visible field labels, validation feedback, and return URLs when switching between them. External sign-in options are shown only when providers are configured. `Shared/ManageLayout.razor` uses the wide card variant for responsive settings navigation and content; recovery codes, notices, and destructive actions share the card styles.
- Password and external-provider registration both require either a home name or an invitation code. The Application `IHomeRegistration` contract is implemented in Infrastructure, which commits Identity creation, membership, and invitation redemption in one database transaction. Confirmation email handling runs after that commit and retains the existing behavior.
- Owners cannot delete their account while they own a home; the UI explains this and a database foreign key prevents orphaned homes. Ownership transfer and home deletion are not yet implemented. Deleting a non-owner account removes its membership.

- Keep existing `/Account/...` route URLs, form actions, query parameters, and callback values stable when moving files.
- `_Imports.razor` excludes account pages from interactive routing. `Manage/_Imports.razor` applies authorization and the account-management layout.
- Account forms rely on static SSR and antiforgery tokens. Login uses the application cookie and external authentication uses the external cookie.
- `Shared/PasskeySubmit.razor.js` is loaded from `Features/Account/Shared/PasskeySubmit.razor.js` by `Components/App.razor`.
- Confirmed accounts are required; the existing no-op email sender and inline registration-confirmation link are retained from the template. Email is not delivered. Replace the sender and remove the inline confirmation-link branch in `RegisterConfirmation` when implementing real email delivery.
- Migration `00000000000000_CreateIdentitySchema` and its schema operations are preserved. EF discovers migrations from the Infrastructure assembly containing the context.
- Unraid deployment preserves data-protection keys in the existing `homehub_homehub-data-protection` volume. Infrastructure applies pending migrations before serving traffic when `Database:ApplyMigrations` is enabled. Database readiness checks connectivity and pending migrations.
- The LAN deployment uses HTTP with HTTPS redirection disabled explicitly. Passkeys require a secure browser context and cannot be exercised over the LAN HTTP URL.

## Verification

Build the solution, then run the model check documented in the repository README. A structural refactor should introduce no pending model changes.

Run the Web app and check that `/Account/Login`, `/Account/Register`, and `/Account/ForgotPassword` render forms with antiforgery tokens; anonymous requests to `/Account/Manage` and `/Account/Manage/Passkeys` redirect to login; and the passkey JavaScript asset loads. Protected management POST endpoints must challenge anonymous callers, and account POST endpoints must reject missing antiforgery tokens. The passkey-option endpoints explicitly call `ValidateRequestAsync`; their existing behavior throws an antiforgery exception for invalid tokens.

With a configured test database and browser, also exercise registration, confirmation, login/logout, password reset, two-factor authentication, and passkeys. External-login tests require a configured provider. Do not claim these complete flows were tested based only on anonymous route checks.
