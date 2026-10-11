# Households

Persistence distinguishes a person's assignable profile from an account's access link. Membership is keyed by Identity user ID (at most one household per account); the profile link belongs to that same household. A filtered unique index permits at most one owner per household. Use cases preserve exactly one owner while creating or transferring ownership.

Invitation codes will be stored as SHA-256 hashes, redeemed once and expired after seven days. Removed profiles are retained for future assignment history and excluded from active member choices. Household registration and management are implemented in subsequent Restart issues.

Owner-only invitations are managed at /Household/Invitations. Codes are generated from 128 random bits, shown once, stored only as hashes, expire after seven days, and can be revoked. /Household/Accounts lists account members; only the owner can remove another account. Removal atomically retires its profile and deletes its access link while retaining Identity. Mutations serialize on the household row and revalidate membership after acquiring the lock.

All active account members can manage non-login profiles at /Household/Members. Account-linked profiles are listed but access is managed separately. Retiring a non-login profile preserves its stable ID and excludes it from GetActiveMembersAsync choices; retired profiles cannot be edited.

/Household/Ownership allows the current owner to transfer to another active account member. Two owner-flag saves run in one transaction under the household lock, preserving the unique owner constraint. /Household/Leave retires the leaving profile and removes access while retaining Identity; the owner cannot leave before transfer. Account deletion requires leaving first, so deleting Identity cannot bypass ownership rules.

The dashboard is at /Dashboard; /Household is the management entry point. Household HTTP routes redirect authenticated accounts without membership to /Household/Onboarding. The interactive app layout rechecks access on navigation and every five seconds; all data operations revalidate immediately. Onboarding lets existing accounts create or join without registering again; account settings remain accessible. No meal or cleaning functionality is included.
