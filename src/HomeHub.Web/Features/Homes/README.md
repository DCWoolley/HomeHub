# Homes

The protected `/Home` page uses the shared application sidebar, with home overview and account settings navigation. Public landing and registration pages retain their standalone layout.

Registration requires exactly one of a home name (up to 100 characters) or an invitation code. A new home gives its creator ownership and membership. Joining an existing home gives membership only. Each user belongs to at most one home; home names need not be unique.

Authenticated users can visit `/Home` from the landing page. Existing accounts with no membership can create or join a home there. Owners can generate invitations on this page; members cannot. The service checks ownership using the authenticated user ID, not a submitted home ID or role. Static server-rendered forms use antiforgery protection.

**Configure home** at `/Home/Configure` is linked from `/Home`. Owners can rename their home, add named floors, and add named rooms to a selected floor. Names are required, trimmed, and limited to 100 characters; floors and rooms are shown alphabetically. Members can view the structure but cannot change it. Every mutation checks the authenticated owner in the service, and rooms can only be added to floors in that owner's home. Separate static SSR forms have independent validation and antiforgery protection. Floor/room renaming, removal, reordering, and ownership transfer are outside this feature.

`AddHomeFloorsAndRooms` adds floor and room tables with required parent foreign keys. Existing homes start with no floors. The migration preserves existing homes, accounts, memberships, and invitations.

Invitations contain 128 random bits, shown as 32 hexadecimal characters. They are single-use and expire seven days after generation (UTC). Only SHA-256 hashes are stored. The code is shown once for the owner to share; generating a new code does not invalidate earlier unused codes. Redemption and registration are transactional, and an optimistic concurrency check prevents two registrations from consuming the same code. No invitation email is sent automatically.

Owners can view invitation expiry/status and revoke individual invitations on `/Home`. Revocation participates in redemption's concurrency check and never removes existing memberships. Code plaintext cannot be recovered after creation. Registration, external-registration and `/Home` POSTs share a ten-per-minute fixed-window limit per client IP, held in the single application instance; excess requests return 429 with `Retry-After`. GET requests are unrestricted. Invalid codes do not reveal a home name. Deployments behind a proxy must configure trusted forwarded headers before relying on client-IP differentiation.

## Household members

`/Home/Members` lists active members and distinguishes the owner, account-linked members and non-user profiles. Owners can add profiles at `/Home/Members/New`, edit them at `/Home/Members/Edit/{id}`, and remove other members at `/Home/Members/Remove/{id}`. Removal requires a named confirmation checkbox. Non-user profiles have a required display name (up to 100 characters) and an optional `#RRGGBB` colour, with no credentials. Profile edits retain the same ID. Account-linked profile names are initially derived from the account username.

`HomeMember` stores the durable profile. `HomeMembership` grants a user current access and links to that profile. Removal retires the profile and deletes the access link in one save, retaining the account and profile for historical references. Every home read checks current membership, so an already signed-in removed user cannot read their former home. Owners cannot remove themselves. Deleted non-owner accounts also lose membership; their retained profiles are excluded from active member lists. There is no restore action: rejoining requires a new valid invitation and creates a new profile. Joining never merges with an existing non-user profile.

Application's `IHomeMembers.GetMembersAsync` returns only active members of the caller's home, including non-user profiles, for future assignment controls. Cleaning and calendar features have not yet been implemented; their use cases must also validate selected member IDs and current access at commit time. No ownership transfer, multiple-home membership, or profile-to-account conversion is included.

Owners can select the household timezone under `/Home/Configure`; names and timezone IDs are validated server-side. `AddHouseholdMemberProfiles` backfills one account-linked profile per existing membership and sets existing homes to UTC, preserving users, ownership, floors, rooms and invitation hashes. Back up the database before deployment. The optional LocalDB migration regression test is enabled with `HOMEHUB_TEST_LOCALDB=1` and uses only a temporary database.

Tests cover non-user creation/editing/retirement, owner protection, cross-home IDs, removed-member access, explicit rejoining, invalid colours/timezones, revocation versus stale redemption, HTTP named confirmation, existing login sessions, and rate limiting. Password and external registration retain account-confirmation requirements; the default sign-in destination is `/Home`.

Domain owns homes, membership and invitation rules. Application exposes registration and home-access contracts and validates the home choice. Infrastructure owns Identity integration, transactions and EF persistence. `Components/Shared` contains the card and home-choice form controls reused by account registration and home management.

`AddHomesAndInvitations` adds tables and preserves existing accounts. Existing users choose a home after signing in; migration does not assign them to a home automatically. Owners must keep their accounts while they own a home; ownership transfer and home deletion are outside this feature.

Tests use an isolated SQLite database for transactional registration and complete HTTP password-registration/sign-in/invitation flows. They cover invalid choices, invalid/expired/reused invitations, member permissions, rollback, external-account persistence, required authentication and antiforgery. Configuration tests cover persisted names and hierarchy, validation, member restrictions, cross-home floor protection, and complete HTTP form submissions. External-provider browser flows require a configured provider.
