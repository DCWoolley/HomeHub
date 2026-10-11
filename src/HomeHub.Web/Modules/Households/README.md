# Households

## Access and profiles

Registration either creates a household and its owner or joins with a valid code. The Identity account, profile, access link and invitation redemption commit together. A failed registration leaves no new standalone account.

HouseholdMembership is keyed by Identity user ID, so an account can have only one household. HouseholdMember is the stable assignable person profile. A composite foreign key keeps the access link attached to a profile in the same household. A filtered unique owner index permits at most one owner; use cases preserve exactly one owner.

All active account members can create, rename and retire profiles without logins. Account-linked profiles appear in active member choices but their access is managed separately. Retirement preserves the profile ID and excludes it from active choices. Non-login profiles cannot authenticate or own the household.

Only the owner can invite or remove account members. Removal atomically deletes access and retires the profile; Identity remains. Members can leave, but an owner must first transfer ownership to another active account member. Identity deletion requires leaving first.

## Invitations and concurrency

Codes contain 128 random bits, are shown only at creation, and are stored as SHA-256 hashes. They expire seven days after creation, can be revoked, and can admit exactly one account. Redemption uses a conditional database update inside the registration/join transaction.

Household mutations acquire a write lock by updating the household Version before rechecking membership. Joining, revocation, removal, leaving and transfer serialize on that row. Ownership transfer saves the two owner flags in one transaction, respecting the filtered unique index. Client role checks only affect presentation; use cases enforce authorization again.

## Entry points

| Route | Purpose |
| --- | --- |
| /Household | Household summary and navigation |
| /Household/Members | Everyone can manage non-login profiles |
| /Household/Accounts | List account members; owner can remove access |
| /Household/Invitations | Owner creates and revokes single-use codes |
| /Household/Ownership | Owner transfers to an active account member |
| /Household/Leave | Leave after transferring ownership if necessary |
| /Household/Onboarding | Existing accounts without membership create or join |
| /Dashboard | Protected dashboard shell |

Household HTTP requests redirect accounts without membership to onboarding. Account settings remain available. Each server-side data operation revalidates membership, so retained cookies and an already-open circuit cannot authorize a removed user. AppLayout also checks on navigation and every five seconds to clear the visible household shell.

The [Meal Planner](../MealPlanner/README.md) shares meals and dated weekly plans across household account members. It currently has no profile assignments. Cleaning and profile assignment remain future work.
