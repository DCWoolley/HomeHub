# Households

Persistence distinguishes a person's assignable profile from an account's access link. Membership is keyed by Identity user ID (at most one household per account); the profile link belongs to that same household. A filtered unique index permits at most one owner per household. Use cases preserve exactly one owner while creating or transferring ownership.

Invitation codes will be stored as SHA-256 hashes, redeemed once and expired after seven days. Removed profiles are retained for future assignment history and excluded from active member choices. Household registration and management are implemented in subsequent Restart issues.
