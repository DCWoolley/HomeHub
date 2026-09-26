# Homes

Registration requires exactly one of a home name (up to 100 characters) or an invitation code. A new home gives its creator ownership and membership. Joining an existing home gives membership only. Each user belongs to at most one home; home names need not be unique.

Authenticated users can visit `/Home` from the landing page. Existing accounts with no membership can create or join a home there. Owners can generate invitations on this page; members cannot. The service checks ownership using the authenticated user ID, not a submitted home ID or role. Static server-rendered forms use antiforgery protection.

Invitations contain 128 random bits, shown as 32 hexadecimal characters. They are single-use and expire seven days after generation (UTC). Only SHA-256 hashes are stored. The code is shown once for the owner to share; generating a new code does not invalidate earlier unused codes. Redemption and registration are transactional, and an optimistic concurrency check prevents two registrations from consuming the same code. No invitation email is sent automatically.

Domain owns homes, membership and invitation rules. Application exposes registration and home-access contracts and validates the home choice. Infrastructure owns Identity integration, transactions and EF persistence. `Components/Shared` contains the card and home-choice form controls reused by account registration and home management.

`AddHomesAndInvitations` adds tables and preserves existing accounts. Existing users choose a home after signing in; migration does not assign them to a home automatically. Owners must keep their accounts while they own a home; ownership transfer and home deletion are outside this feature.

Tests use an isolated SQLite database for transactional registration and complete HTTP password-registration/sign-in/invitation flows. They cover invalid choices, invalid/expired/reused invitations, member permissions, rollback, external-account persistence, required authentication and antiforgery. External-provider browser flows require a configured provider.
