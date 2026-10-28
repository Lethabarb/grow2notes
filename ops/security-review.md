# Security review

The OWASP ASVS 5.0.0 level 1 and 2 self-review of Grow2Notes, with its findings, the check of the controls in
design.md §8 and §9, and the ZAP baseline results (S06.01.01, S06.01.02; design.md §14 M6).

## 1. About this review

- **Standard.** OWASP Application Security Verification Standard (ASVS) 5.0.0, levels 1 and 2. The requirement IDs and
  levels come from the standard, which is licensed CC BY-SA 4.0 (https://creativecommons.org/licenses/by-sa/4.0/); the
  topic words beside each ID are ours, not the standard's. Source: https://github.com/OWASP/ASVS, folder `5.0/en` at
  the v5.0.0 tag.
- **Level 3 is out of scope.** The standard's level 3 requirements are counted in section 5 and not reviewed.
- **What was reviewed.** `main` at commit `9f7514f`, plus T25's changes: the rate limits (V1) and
  `.github/dependabot.yml`. F-1's fix was checked on 25 October 2026 against the pull request "fix(auth): sign-out
  ends the session for good".
- **Date.** 24 October 2026.
- **Scope.**
  - The app: `src/Grow2Notes.Web` (the `Platform`, `Features/Auth` and `Data` folders, the export endpoints, the
    last-manager rule and the note locking, and `Program.cs`) and the SPA in `src/grow2notes-spa` (its API client,
    sign-in and setup screens, and build scripts). The other endpoints were reviewed through the endpoint matrix and
    the tenancy tests, which call every route as every kind of user.
  - The infrastructure: every file in `infra/*.bicep` and `infra/modules/`, and `infra/sql/*.sql`.
  - The pipeline: `.github/workflows/ci.yml`, `.github/workflows/deploy.yml`, `.github/rulesets/protect-main.json`
    and `.github/dependabot.yml`.
  - Package and tool settings: `Directory.Build.props`, `Directory.Packages.props`, `global.json` and
    `.config/dotnet-tools.json`.
- **Method.** By reading, not by running. Each row below was written from the code, the configuration and the tests
  as they stand; nothing was executed, scanned or deployed for this review. A test named as evidence is one that
  passes in the green CI run of the pull request that added this record. A row that needs a running system says so
  and names the step in `ops/first-use-in-test.md` that confirms it.
- **How the IDs were taken.** Each chapter file of the standard was read twice, from the v5.0.0 tag, asking for every
  requirement ID and its level. The two reads agreed for every chapter, the numbers in every section run without a gap,
  and section 5's counts are the counts read. Nothing from the standard was saved to disk.
- **What this is not.** It is not a penetration test, and the infrastructure rows describe the Bicep files, which the
  deploy applies on every push to `main` (`deploy.yml`), not a look at the Azure portal.
- **The standard for "done".** design.md §14 M6 *Scope* (Security) and *Done when* (no high or critical finding
  open) are the test this record answers; they are linked here, not copied.
- **Re-review.** A row that cites a file that T22, T24 or T26 later change needs a second look at that row.
- **Dependency policy (15.1.1).** A high or critical advisory fails CI (NuGet Audit, `npm audit`) and is fixed before
  the change merges; anything lower arrives in the weekly grouped Dependabot update (design.md §9.11).

## 2. Severity scale

design.md does not define one, so this review does:

| Severity | Meaning |
|---|---|
| Critical | Remote, unauthenticated access to participant data or to accounts. |
| High | The same, with little effort, or from any signed-in account. |
| Medium | Needs unusual conditions, or removes a layer of defence in depth. |
| Low | Hardening, with little direct risk. |
| Informational | No risk. |

T25 does not merge to `main` while a High or Critical finding is open (F06.01's rule; F06.05's gate).

## 3. Findings

**Open High/Critical: none.**

No Medium finding is open: F-1 was fixed on 25 October 2026, at the owner's decision. Every finding is recorded with a
decision. "Accepted" means the developer decided to leave it, and the owner may overturn that. "Fixed" means the owner
asked for the change and it is made; the row says how. No finding is in `infra/` at High or Critical, so no bug story
was filed; `infra/` was edited afterwards only to fix a finding at the owner's request (F-25 and F-8: `bootstrap.bicep`).
Counts: Medium 1 (fixed); Low 17; Informational 7.

### 3.1 Register

| ID | Severity | ASVS | Where | What | Decision | Status |
|---|---|---|---|---|---|---|
| F-1 | Medium | 7.4.1 | `LogoutEndpoint.cs`, `Program.cs` | Sign-out deletes the browser's cookie and sends `Clear-Site-Data`, but the session is a stateless protected ticket, so a copy of the cookie keeps working until it expires (30 minutes idle, 12 hours) or the person's security stamp changes. Getting a copy needs the cookie to be stolen despite Secure, HttpOnly and SameSite=Strict. | The owner decided on 25 October 2026 to fix it, not accept it. Fixed: sign-out gives the person a new security stamp in one transaction with `auth.signed_out`, so a copy of the cookie gets 401 on its next request, as does every other session of theirs (design.md A99; `LogoutEndpointTests.A_copy_of_the_session_cookie_gets_401_after_sign_out`, `LogoutEndpointTests.Every_other_session_of_the_person_gets_401_on_its_next_request`; by-hand step 150). | Fixed |
| F-2 | Low | 6.5.1 | Identity's authenticator provider | A TOTP code can be used again while its time window lasts: Identity checks a few time steps around now and keeps no record of a code once used. It needs the password as well. | Accepted: the account limit of 10 a minute and the lockout bound guessing, and a used-code store means a new table. | Accepted |
| F-3 | Low | 6.1.2, 6.2.4, 6.2.11, 6.2.12 | `Program.cs` | New passwords are checked for length (12) only; there is no common-password, breached-password or context-word check. | Accepted: every account has a second factor, and lockout and the rate limits slow guessing. | Accepted |
| F-4 | Low | 6.2.2, 7.5.1, 7.5.2 | design.md §8.6 | No self-service account management: a person cannot change their password, or list their sessions or end one alone (Sign out ends them all, A99), and a manager is not asked to sign in again before changing a person's email or resetting their sign-in. | Accepted, by design: recovery goes through a manager (D23, D74); a reset is confirmed in a dialog and audited. | Accepted |
| F-5 | Low | 11.4.2 | `Program.cs` | Passwords are hashed with Identity's default PBKDF2 at its default of 100,000 iterations, below OWASP's current guidance for HMAC-SHA512. | Accepted: it matters only to someone who already has a copy of the database, and the second factor remains. Raising `PasswordHasherOptions.IterationCount` is one line, and Identity rehashes at the next sign-in. | Accepted |
| F-6 | Low | 3.3.1, 3.3.2, 3.3.3 | `Program.cs`, `EnrolmentCookie.cs` | Identity's two-factor cookie keeps its defaults (SameSite Lax, Secure as the request is, no `__Host-` prefix), and the setup cookie is `__Secure-` with a path, not `__Host-`. | Accepted: both are HttpOnly, last 5 and 30 minutes, hold a protected ID, and every endpoint that reads them needs the antiforgery token; the setup cookie's path is deliberate. Setting `SecurePolicy` and `SameSite` on the two-factor scheme is two lines beside the existing `Configure` call. | Accepted |
| F-7 | Low | 11.2.3 | `infra/modules/keyvault.bicep` | The Key Vault key that wraps the Data Protection key ring is RSA 2048, about 112 bits of security where the standard asks for 128. | Accepted, `infra/` not edited: the key only wraps the key ring, which is itself AES-256. A 3072-bit key is a change to the vault module and a re-wrap, best made when production is first deployed. | Accepted |
| F-8 | Low | 13.2.2 | `infra/modules/bootstrap-environment.bicep` | The app identity held the built-in role Communication and Email Service Owner, to send mail with Entra ID, which is broader than sending. | Fixed (D92, 25 October 2026): Communication Services is gone, and the role is replaced by Key Vault Secrets User at group scope, which only reads secrets, to read the Resend key (`infra/modules/bootstrap-environment.bicep`). The old role assignment is deleted by hand (`infra/README.md`, `ops/first-use-in-test.md` step 153). | Fixed |
| F-9 | Low | 13.2.3 | `.github/workflows/ci.yml`, `tests/README.md` | CI and the test guide hold a fixed SQL Server password for a throwaway container. The repository is public. | Accepted: the password guards a container that exists for one CI job and is reachable only inside it; no deployed system uses it. | Accepted |
| F-10 | Low | 13.2.4 | `.github/workflows/deploy.yml` | The deploy asks a third-party service for the runner's address, to open a temporary SQL firewall rule. | Accepted: the answer must look like an IPv4 address, the rule is deleted after the migration whatever happens, and SQL accepts Entra sign-in only, so the firewall alone gives no access. | Accepted |
| F-11 | Low | 2.4.1, 15.2.2 | `HealthEndpoints.cs` | `/healthz/ready`, the probe production uses, is open to everyone, has no rate limit and runs a one-row query for each call. | Accepted: the query is `SELECT 1` with a 5-second timeout, and a flood is a platform-level denial of service that no app limit stops. Test's `/healthz` touches no database. | Accepted |
| F-12 | Low | 14.1.2 | `Program.cs` | Authenticator keys are stored as Identity stores them, readable in its user-token table (its personal-data encryption is off), so a reader of the database holds password hashes and keys together. | Accepted: the database is encrypted at rest and reachable only from the app's subnet and the SQL admin group (design.md §9.3, §9.4). Encrypting the keys needs Identity's personal-data protection and its key handling, a larger change. | Accepted |
| F-13 | Low | 6.3.8 (level 3) | `PasswordSignInEndpoint.cs` | A56: an email of no account is answered faster than a known one. | Accepted (A56): bounded by 30 requests a minute for each address and 10 for each email; equal times would need a hash for emails of no account. | Accepted |
| F-14 | Low | 6.3.1 | `AccountRateLimiter.cs` | Anyone who knows an email can keep that account's password step answering 429 by sending 10 requests a minute. | Accepted (A95): no cheaper than the lockout, where 5 wrong passwords pause the account for 15 minutes (A25); passkey sign-in and the code step are not affected; each sender's effort is bounded by its own address limit. | Accepted |
| F-15 | Low | 2.4.1 | `AccountRateLimiter.cs` | Emails that SQL Server's comparison treats as one beyond letter case and trailing spaces (which the limiter folds) get separate counts. | Accepted (A95): each variant costs its sender a request of the address limit, and every failure still counts towards that one account's lockout. | Accepted |
| F-16 | Low | 2.4.1 | `RateLimits.cs` | Client addresses are counted one by one, so an IPv6 client could spread requests over the addresses of its /64. | Accepted: the account limit and the lockout do not depend on the address; revisit if inbound IPv6 is turned on. | Accepted |
| F-17 | Low | 2.4.1 | `RateLimits.cs`, `AccountRateLimiter.cs` | The counts are in memory, so a restart or a deploy resets them, and a second instance would count on its own. | Accepted (A95): the app runs one instance (design.md §10.1); the lockout is in SQL and survives. | Accepted |
| F-18 | Low | 6.4.1 | `SetupTokenProviderOptions.cs` | The setup link lives 7 days (A23); the standard says a short time. | Accepted (A23, the owner's decision): staff may be rostered off when invited. The link is single use, ends when the security stamp changes (a resend or a reset), and sits in the person's own mailbox; a manager can resend it. | Accepted |
| F-19 | Informational | none | `RateLimits.cs` | Capacity of a shared office address: the 30 a minute is shared by everyone behind it, and a password sign-in uses about 3 of them, so about 10 sign-ins a minute. | Accepted (A96): §9.9's number stays; the owner is asked whether larger offices need more (T25 Notes); step 142 names the case. | Accepted |
| F-20 | Informational | 13.4.6 (level 3) | Kestrel | Responses carry `Server: Kestrel`, which names the software without a version. | Accepted, as the S06.01.04 Notes decided: design.md §9.7 does not mention it, and an alert from ZAP's rule is decided at step 141. Removing it is one Kestrel option. | Accepted |
| F-21 | Informational | 6.3.1 | `SetupPasswordEndpoint.cs` | Setup's code check has no account limit. | Accepted (A96): whoever reaches it holds the enrolment cookie, which only a valid setup link gives, and can already see the authenticator key on that screen, so guessing adds nothing. | Accepted |
| F-22 | Informational | 15.2.1 | `.github/dependabot.yml` | Dependabot has no entry for the SQL Server image tag and Node's version in `ci.yml`, or the SDK pin in `global.json`. | Accepted: they change by hand, when a release matters. | Accepted |
| F-23 | Informational | none | GitHub settings | The repository is public. Settings that are not files (who may run a workflow from a fork, the branch rules of the `test` and `prod` environments, the default token permission, secret scanning) are outside `.github/` and were not read. Each workflow sets its own permissions, so none depends on the default. | Owner note in T25's Notes: look at them in GitHub, including that `test` and `prod` take deployments from `main` only (D94). CodeQL and secret scanning are free on a public repository and stay off unless the owner turns them on. | Accepted |
| F-24 | Informational | 12.1.2 | App Service | The TLS cipher suite list is Azure's platform default; the Bicep sets the minimum version only. | Accepted: nothing in the steps tests it; a minimum cipher suite can be set in the web app module later. | Accepted |
| F-25 | Informational | 13.3.1 | `infra/bootstrap.bicep` | The default of the GitHub subject prefix named the GitHub account and the numeric IDs of the account and the repository. They are public identifiers, not secrets, but the project's rule is that no ID is in the repository. | Fixed (owner, 25 October 2026, D89): `githubSubjectPrefix` has no default and refuses an empty value; the operator reads the prefix with `gh api` and passes it when running the bootstrap (`infra/README.md` steps 2 to 4), and step 150 of `ops/first-use-in-test.md` checks that the what-if shows no change. No file holds the values now; they stay in git history, as `main` is never rewritten. | Fixed |

### 3.2 Inputs the design left to this review

| Input | Decision |
|---|---|
| No rate limits (S06.01.05) | Fixed in T25's first vertical: the four limits of design.md §9.9, proved by `RateLimitsTests`; step 142 checks them on the live site. |
| No Dependabot (S06.01.03) | Fixed: `.github/dependabot.yml`; alerts are turned on at step 140. |
| A55: wrong passwords on an account with no password count for nothing | Confirmed, no finding: `PasswordSignInLimitsTests.Wrong_passwords_for_an_account_with_no_password_lock_nothing_and_its_passkey_still_signs_in`. The account limit also bounds those requests. |
| A56: how soon the password step answers | F-13. |
| Failed attempts sent at once that Identity counts once (S06.01.01 Notes) | Bounded, no finding: the account limit of 10 a minute caps guesses at one account however fast they come (`RateLimitsTests.The_password_step_allows_10_a_minute_for_each_account_by_its_normalised_email`; `PasswordSignInLimitsTests.Wrong_passwords_sent_at_once_are_each_a_failed_sign_in_with_its_event`). |
| The `Server: Kestrel` header | F-20. |
| The in-memory, single-instance limiter | F-17. |
| Email variants that SQL Server treats as one | F-15. |
| Denial of a known email's password step | F-14. |
| Grouping IPv6 clients by /64 | F-16. |
| Shared-office capacity | F-19. |
| Setup's code step | F-21. |
| The fixed SQL password in CI and the test guide | F-9 (the value is not repeated here). |
| Idle per-key limiters | No finding: the built-in limiter drops a window that has gone idle, and a key is made only after the address limit lets the request through, and the password step's key is cut to the email column's size (256), so a key cannot be large. |
| The public repository's workflows | Checked in section 4.3: no finding beyond F-9, F-10 and F-23. |

## 4. Controls of design.md §8 and §9

### 4.1 Controls

| Control | In place | Evidence |
|---|---|---|
| §8: passkey sign-in, user verification required | Yes | `Program.cs` (`UserVerificationRequirement`, `ResidentKeyRequirement`, the origin check of A54); `PasskeySignInEndpointTests`; step 9 |
| §8: password of 12 or more characters plus an authenticator app | Yes | `Limits.PasswordMinLength`; `SetupPasswordEndpointTests`; `TotpSignInEndpointTests`; step 14 |
| §8.1 invariant: a password account always has TOTP | Yes | `Grow2NotesSignInManagerTests.The_check_before_signing_in_fails_a_password_user_without_two_factor`; `SetupPasswordEndpointTests.After_completion_a_password_sign_in_requires_the_code_and_starts_no_session` |
| §8: setup link, single use, 7 days | Yes (F-18) | `SetupTokenProviderTests.The_provider_that_UserManager_uses_for_setup_tokens_is_built_with_a_7_day_lifespan`; `SetupStartEndpointTests`; `SetupPasswordEndpointTests.After_completion_the_same_link_gets_410_and_a_second_completion_401` |
| §8: enrolment cookie, 30 minutes, only on `/api/auth/setup/*` | Yes | `EnrolmentCookie.cs`; `SetupStartEndpointTests.The_enrolment_cookie_holds_the_user_s_ID_and_stamp_for_30_minutes_with_no_renewal` |
| §8: idle timeout 30 minutes, absolute limit 12 hours | Yes | `SessionLimitTests`; step 11 |
| §8: two-factor code step 5 minutes, not renewed by a wrong code | Yes | `PasswordSignInLimitsTests.The_right_code_4_minutes_and_59_seconds_after_the_password_signs_in`, `After_a_wrong_code_at_3_minutes_the_right_code_at_5_minutes_and_1_second_gets_a_401` |
| §8: no "keep me signed in" | Yes | `isPersistent: false`, `rememberClient: false` (`TotpSignInEndpoint.cs`) |
| §8: lockout, 5 failures, 15 minutes | Yes | `PasswordSignInLimitsTests.Five_wrong_passwords_lock_sign_in_for_15_minutes_and_the_right_password_then_gets_a_401` |
| §8: recovery by a manager's Reset sign-in only | Yes | `SignInResetTests`; `UserDeactivationEndpointsTests` |
| §8.6: deactivation and reset end every session; the last manager cannot be removed | Yes | `UserDeactivationEndpointsTests.Each_device_of_a_deactivated_worker_gets_401_on_its_next_request_and_their_passkey_too`; `SignInResetTests.The_only_active_manager_resetting_themselves_gets_409_user_last_manager_and_nothing_is_done` |
| §9.1: deny by default, a Manager policy, record-level checks | Yes | `Policies.cs`; `PoliciesTests`; `EndpointMatrixTests.Every_endpoint_answers_the_caller_with_the_status_in_its_row`; `SignedOutTests` |
| §9.2: tenant isolation | Yes | `ModelTenancyTests`; `TenantIsolationOnNewEntitiesTests`; `IgnoreQueryFiltersSourceTests` |
| §9.3: browser to app over HTTPS, TLS 1.2 or later, HSTS for a year with `includeSubDomains` | Yes | `infra/modules/app.bicep` (`httpsOnly`, `minTlsVersion`); `HstsTests`; step 141 |
| §9.3: app to database with `Encrypt=Strict`, server TLS 1.2 | Yes | `infra/main.bicep` (the connection string); `infra/modules/sql.bicep` (`minimalTlsVersion`) |
| §9.3: encryption at rest | By the platform | Azure SQL's transparent data encryption is on by default and is not set in the repository; the restore drill (`ops/restore-drill.md`) covers the backups |
| §9.3: key ring in SQL, wrapped by a Key Vault key; soft delete and purge protection on | Yes | `KeyRingWrapping.cs`, `KeyRingWrappingTests`; `infra/modules/keyvault.bicep`; `infra/sql/check-database.sql` check C |
| §9.4: managed identities; one long-lived secret, the Resend key, in Key Vault and in each environment's GitHub secret `RESEND_API_KEY` (D94), passed as a secure parameter | Yes | `infra/main.bicep` (app settings hold IDs, addresses and the secret's URI only); `infra/modules/keyvault.bicep`; `InfraTemplateTests`; `infra/modules/bootstrap-environment.bicep` |
| §9.4: GitHub to Azure by OIDC, per environment | Yes | `infra/modules/bootstrap-environment.bicep` (the credential's subject names the environment); `deploy.yml` |
| §9.4: runtime role with grants and denies, append-only history and audit | Yes | `RuntimeRoleTests`; `AuditEventAppendOnlyTests`; `infra/sql/grant-identities.sql`; `infra/sql/check-database.sql` checks A, B, D and E |
| §9.4: FTP and basic-auth publishing off | Yes | `infra/modules/app.bicep` (`ftpsState`, `basicPublishingCredentialsPolicies`) |
| §9.5: IDs only in logs and telemetry; no browser telemetry | Yes | `TelemetryCanaryTests.A_canary_in_request_bodies_and_SQL_error_text_appears_in_no_log_telemetry_or_response`; `DatabaseFailureHandlerTests`; `ci.yml` (`check:no-analytics`) |
| §9.5: workspace in Australia, 30 days, daily cap, local auth off | Yes | `infra/modules/monitoring.bicep` |
| §9.6: no-store, no storage on the device, `Clear-Site-Data` at sign-out | Yes | `CacheHeadersTests`; `LogoutEndpointTests.Answers_204_with_Clear_Site_Data_and_no_store_and_deletes_the_session_cookie`; steps 7 and 61 |
| §9.7: the six security headers on every response | Yes | `SecurityHeadersTests` (including `A_429_of_a_rate_limit_carries_the_headers_of_design_md_9_7_once_each_with_their_values`); `DesignedSecurityHeadersTests`; `SpaBuildTests`; step 141 |
| §9.8: CSRF | Yes | `ApiGroupTests`; `AntiforgeryEndpointTests` |
| §9.9: the four rate limits and the lockout | Yes | `RateLimitsTests` (all seven); `PasswordSignInLimitsTests`; step 142 |
| §9.10: breach response | Yes | `ops/breach-response.md`; `SignOutAllCommandTests`; `ops/audit-extraction.md` |
| §9.11: NuGet Audit, `npm audit`, SHA-pinned actions | Yes | `Directory.Build.props`; `ci.yml`; every `uses:` in `ci.yml` and `deploy.yml` names a commit |
| §9.11: Dependabot grouped weekly updates and alerts | Yes | `.github/dependabot.yml`; alerts at step 140 |
| §9.11: CodeQL and secret scanning | Not built | Optional in the design; the owner may turn them on (F-23) |

### 4.2 Cryptographic inventory (11.1.2)

| What | Algorithm and size | Kept in | Protected by |
|---|---|---|---|
| Session, antiforgery, enrolment and two-factor cookies; setup tokens | AES-256-CBC with HMAC-SHA256 (Data Protection's default) | The key ring in `dbo.DataProtectionKeys` | The Key Vault key below |
| Key ring wrapping key `data-protection` | RSA 2048, wrap and unwrap only, OAEP (F-7) | Key Vault, with soft delete and purge protection | Entra RBAC: the app identity's Crypto Service Encryption User role |
| Resend API key | Opaque bearer token | Key Vault secret `resend-api-key`, written by each deploy from the environment's GitHub secret `RESEND_API_KEY` and read at each send | Entra RBAC: the app identity's Key Vault Secrets User role |
| Passwords | PBKDF2 with 100,000 iterations (F-5) | `AspNetUsers` | Database access control |
| Authenticator keys | HMAC-SHA1 TOTP of RFC 6238, 6 digits (F-12) | Identity's user-token table | Database access control |
| Passkeys | WebAuthn public keys, user verification required | Identity's passkey table | Database access control |
| Browser to app | TLS 1.2 or later, certificate of App Service (its own, in both environments, D91) | The platform | Azure |
| App to database | TDS 8 strict encryption, TLS 1.2 or later | The platform | Azure SQL's certificate |
| Data at rest | Azure SQL's transparent data encryption, service-managed key | Azure | Azure |
| Azure access from GitHub | OIDC tokens, nothing stored | GitHub and Entra | Per-environment subject |

### 4.3 The public repository's workflows

| Check | Result | Where |
|---|---|---|
| No `pull_request_target` and no workflow that runs a pull request's code with secrets | Met: CI runs on `pull_request` and `push`, and uses no secret | `ci.yml` |
| Least-privilege token | Met: `contents: read` for CI; `permissions: {}` for deploy, with `id-token` and `actions: read` on the one job that needs them | `ci.yml`, `deploy.yml` |
| Deploys only from `main` | Met: `workflow_run` of CI, `branches: [main]`, only for a successful `push` run, so a fork's pull request never deploys | `deploy.yml` |
| Deploys the artifact CI built | Met: the artifact comes from that `push` run's ID | `deploy.yml` |
| No untrusted text in a shell command | Met: the values a script uses reach it through `env:` | `deploy.yml` |
| Third-party actions pinned | Met: each `uses:` names a commit, with its version in a comment (kept current by Dependabot) | `ci.yml`, `deploy.yml` |
| No stored credentials | Met: `persist-credentials: false` on checkout; Azure access is by OIDC | `ci.yml`, `deploy.yml` |
| Secrets stay out of the public logs | Met: the IDs and the Resend key are secrets, so the logs mask them, and the deploy masks the identity and telemetry values it reads | `deploy.yml` |
| Main is protected | Met: pull request and the `Build and test` check required, squash merge only, no force push or delete, no bypass actor | `.github/rulesets/protect-main.json` |
| Fixed password in CI | F-9 | `ci.yml` |
| Third-party call in the deploy | F-10 | `deploy.yml` |
| Settings outside the files | F-23 | GitHub |

## 5. ASVS 5.0.0 level 1 and 2 requirements

A row has one status:

- **Met**, with evidence: a file or a test in the repository.
- **Met by design, confirmed in test by step N**: step N of `ops/first-use-in-test.md` confirms it on the running test
  site.
- **N/A**, with the reason.
- **F-n**: a finding of section 3, which says what is not met and what was decided.

Counts are those read from the standard against the rows written below; they must be equal.

| Chapter | L1 read | L2 read | L1 rows | L2 rows | Level 3 read (not reviewed) |
|---|---|---|---|---|---|
| V1 Encoding and sanitization | 8 | 19 | 8 | 19 | 3 |
| V2 Validation and business logic | 4 | 7 | 4 | 7 | 2 |
| V3 Web frontend security | 8 | 11 | 8 | 11 | 12 |
| V4 API and web service | 2 | 8 | 2 | 8 | 6 |
| V5 File handling | 4 | 5 | 4 | 5 | 4 |
| V6 Authentication | 13 | 22 | 13 | 22 | 12 |
| V7 Session management | 6 | 12 | 6 | 12 | 1 |
| V8 Authorization | 4 | 3 | 4 | 3 | 6 |
| V9 Self-contained tokens | 4 | 3 | 4 | 3 | 0 |
| V10 OAuth and OIDC | 5 | 24 | 5 | 24 | 7 |
| V11 Cryptography | 3 | 11 | 3 | 11 | 10 |
| V12 Secure communication | 3 | 6 | 3 | 6 | 3 |
| V13 Configuration | 1 | 12 | 1 | 12 | 8 |
| V14 Data protection | 2 | 7 | 2 | 7 | 4 |
| V15 Secure coding and architecture | 3 | 10 | 3 | 10 | 8 |
| V16 Security logging and error handling | 0 | 16 | 0 | 16 | 1 |
| V17 WebRTC | 0 | 7 | 0 | 7 | 5 |
| **Total** | **70** | **183** | **70** | **183** | **92** |

Of the 253 rows, 153 are met (8 of them confirmed in test), 86 are not applicable and 14 point to a finding.

### V1 Encoding and sanitization

| ID | L | Topic | Status |
|---|---|---|---|
| 1.1.1 | 2 | Input decoded once before use | Met: the framework's binders and System.Text.Json decode request data once; the only hand normalisation is `OneTimeCode.TryRead` on the one-time code (`OneTimeCodeTests`). |
| 1.1.2 | 2 | Output encoded at the last step | Met: responses are JSON from the serialiser, problem details, or PDF and Word files made by libraries; React escapes the text it renders. |
| 1.2.1 | 1 | Encoding for each response type | Met: the server builds no HTML; the page is static files (`SpaBuildTests.The_page_has_no_inline_script_or_style_and_no_data_uri`) and the API answers JSON. |
| 1.2.2 | 1 | Untrusted data in built URLs | Met: the setup link is made from `App:Origin`, the user ID and the token escaped with `Uri.EscapeDataString` (`SetupLinkIssuer.cs`); the SPA's addresses hold IDs and dates only. |
| 1.2.3 | 1 | Script and JSON contexts | Met: no server-written script (`SpaBuildTests`); no `dangerouslySetInnerHTML`, `innerHTML`, `eval` or `document.write` in the SPA source (read). |
| 1.2.4 | 1 | Database queries | Met: EF Core queries are parameterised; the three raw statements (`NoteDrafts.cs` and `NoteVersions.cs` with `FromSql`, `LastManagerRule.cs` with `ExecuteSqlAsync`) take interpolated values, which EF sends as parameters. |
| 1.2.5 | 1 | Operating-system commands | N/A: the app starts no process (no `Process.Start` in `src/`). |
| 1.2.6 | 2 | LDAP injection | N/A: the app uses no LDAP. |
| 1.2.7 | 2 | XPath injection | N/A: the app runs no XPath or XML queries. |
| 1.2.8 | 2 | LaTeX processors | N/A: the app has none. |
| 1.2.9 | 2 | Special characters in regular expressions | N/A: no request text is put into a regular expression; the few expressions are fixed (`ItemText.cs`, the SPA's header parsing). |
| 1.3.1 | 1 | HTML from rich-text editors | N/A: there is no rich-text input; note text is plain text. |
| 1.3.2 | 1 | Dynamic code evaluation | Met: no `eval`, `new Function` or dynamic assembly loading in the SPA or the server (read). |
| 1.3.3 | 2 | Data passed to risky contexts | Met: note text reaches the PDF and Word files as text runs made by the libraries, and SQL as parameters (`PdfReport.cs`, `WordReport.cs`). |
| 1.3.4 | 2 | User-supplied SVG | N/A: the app accepts no files. |
| 1.3.5 | 2 | User-supplied scriptable content | N/A: the app accepts text only and renders no Markdown, CSS or XSL from users. |
| 1.3.6 | 2 | Server-side request forgery | N/A: no request the server makes uses a user-supplied address; its outbound calls go to the configured Azure endpoints. |
| 1.3.7 | 2 | Template injection | N/A: there is no server template engine; the email and the files are built by fixed code with values inserted as text. |
| 1.3.8 | 2 | JNDI injection | N/A: the app is not a Java application. |
| 1.3.9 | 2 | Memcache injection | N/A: the app uses no memcache. |
| 1.3.10 | 2 | Format strings | Met: log messages are source-generated templates with fixed text (`LoggerMessage`); no user text is used as a format. |
| 1.3.11 | 2 | Mail injection | Met: the email goes through Resend's HTTP API as a typed message (`ResendEmailSender.cs`), not SMTP, and the address is checked by `UserDetailsChecks` and Identity. |
| 1.4.1 | 2 | Memory-safe handling | Met: managed C# and TypeScript; no `unsafe` code in `src/` (read). |
| 1.4.2 | 2 | Integer range checks | Met: user numbers are bounded by validation and the `Limits` constants (`SpaLimitsTests`); no unchecked arithmetic on request values. |
| 1.4.3 | 2 | Freed memory | N/A: memory is managed and garbage-collected. |
| 1.5.1 | 1 | XML parsers | N/A: the app parses no XML (no `XmlReader`, `XmlDocument` or `XDocument` in `src/`); Word files are written with the Open XML SDK, never read. |
| 1.5.2 | 2 | Deserialising untrusted data | Met: only System.Text.Json into fixed record types; no `BinaryFormatter` and no type-name handling. |

### V2 Validation and business logic

| ID | L | Topic | Status |
|---|---|---|---|
| 2.1.1 | 1 | Documented input validation rules | Met: design.md §6.1, §7.5 and A94 give the rules; `Limits.cs` holds the sizes and `SpaLimitsTests` keeps the SPA's copy equal. |
| 2.1.2 | 2 | Documented checks across fields | Met: design.md §6 gives them for each endpoint, such as the record export's from and to (A94). |
| 2.1.3 | 2 | Documented business limits | Met: design.md §8 (lockout, session lengths), §9.9 with A95 and A96 (rate limits) and `Limits.cs` (sizes). |
| 2.2.1 | 1 | Input validated against the rules | Met: `AddValidation` (data annotations) and each endpoint's own checks (`ValidationTests`, `ParticipantExportEndpointTests`). |
| 2.2.2 | 1 | Validation enforced on the server | Met: every rule is checked in the API; the SPA's checks only save a round trip. |
| 2.2.3 | 2 | Related fields checked together | Met: the record export checks `to` against `from` and every field error is returned at once (A94, `ParticipantExportEndpoint.cs`). |
| 2.3.1 | 1 | Steps taken in order | Met: the code step needs the two-factor cookie of a password step, and each setup step needs the enrolment cookie of the start and an Invited user (`TotpSignInEndpointTests.A_code_with_no_password_step_before_it_gets_a_401_and_no_event`, `SetupStartEndpointTests`, A53). |
| 2.3.2 | 2 | Business limits enforced | Met: sizes (`Limits.cs`), the 12-hour session (`SessionLimitTests`), lockout (`PasswordSignInLimitsTests`) and the rate limits (`RateLimitsTests`). |
| 2.3.3 | 2 | Transactions that roll back | Met: `InTransactionAsync` (`TransactionsAndRetriesTests.InTransactionAsync_rolls_back_the_work_and_rethrows_a_failure_that_is_not_transient`) wraps setup completion, user changes and note saves. |
| 2.3.4 | 2 | Locking against double use | Met: the last-manager rule locks the organisation row (`LastManagerRule.cs`; `SignInResetTests.Two_managers_resetting_each_other_at_once_give_one_204_and_one_409_user_last_manager`), note drafts are read under an update lock (`NoteDrafts.cs`, `NoteVersions.cs`) and edits carry If-Match tokens (`IfMatchTests`). |
| 2.4.1 | 2 | Anti-automation | Met: the four rate limits and the lockout (`RateLimitsTests`, `PasswordSignInLimitsTests.Five_wrong_passwords_lock_sign_in_for_15_minutes_and_the_right_password_then_gets_a_401`); their known limits are F-11, F-14, F-15, F-16 and F-17 (Low, accepted). |

### V3 Web frontend security

| ID | L | Topic | Status |
|---|---|---|---|
| 3.2.1 | 1 | Content the browser will not misread | Met: every response carries its type and `X-Content-Type-Options: nosniff` (`SecurityHeadersTests`); files go out as attachments with a fixed file name (`DailyReportEndpoint.cs`). |
| 3.2.2 | 1 | Text shown as text | Met: React renders user text as text nodes, and the SPA writes no HTML (read). |
| 3.3.1 | 1 | Secure attribute on cookies | Met by design, confirmed in test by step 141: the session, antiforgery and setup cookies are always Secure (`CookieSignIn.cs`, `AntiforgeryEndpointTests.Sets_the_cookie_HttpOnly_Secure_and_SameSite_Strict_for_the_whole_site`, `SetupStartEndpointTests`); Identity's two-factor cookie is Secure as the request is, which behind App Service is HTTPS by the forwarded scheme (see F-6). |
| 3.3.2 | 2 | SameSite on cookies | F-6: the session, antiforgery and setup cookies are SameSite=Strict; Identity's two-factor cookie keeps its default. |
| 3.3.3 | 2 | `__Host-` prefix | F-6: the session and antiforgery cookies have the `__Host-` prefix; the setup cookie has `__Secure-` and a path, and the two-factor cookie neither. |
| 3.3.4 | 2 | HttpOnly on session cookies | Met: all four cookies are HttpOnly (`Program.cs`, `ApiGroup.cs`, `EnrolmentCookie.cs`; `AntiforgeryEndpointTests`). |
| 3.4.1 | 1 | Strict-Transport-Security | Met by design, confirmed in test by step 141: one year with `includeSubDomains` (`Program.cs`; `HstsTests.A_request_the_front_end_forwarded_from_HTTPS_gets_HSTS`). |
| 3.4.2 | 1 | Cross-origin resource sharing | Met: no CORS is configured (no `AddCors` or `UseCors` in `src/`), so no `Access-Control-Allow-Origin` is sent. |
| 3.4.3 | 2 | Content-Security-Policy | Met by design, confirmed in test by step 141: `SecurityHeaders.cs` sets the policy of design.md §9.7 on every response, with `object-src 'none'`, `base-uri 'none'` and `script-src 'self'` (`SecurityHeadersTests`, `DesignedSecurityHeadersTests`; the build needs no inline script, `SpaBuildTests`). |
| 3.4.4 | 2 | X-Content-Type-Options | Met by design, confirmed in test by step 141: `nosniff` on every response (`SecurityHeadersTests`). |
| 3.4.5 | 2 | Referrer policy | Met: `Referrer-Policy: no-referrer` on every response, and the setup token is in the URL fragment, which no referrer carries (design.md §8.1). |
| 3.4.6 | 2 | Framing | Met: `frame-ancestors 'none'` in the policy; no `X-Frame-Options` by design (`ApiGroup.cs`). |
| 3.5.1 | 1 | Defence against cross-site requests | Met: SameSite=Strict, the antiforgery token and JSON-only bodies on the whole `/api` group (`ApiGroupTests`; design.md §9.8). |
| 3.5.2 | 1 | Cross-origin requests without a preflight | Met: no CORS, and a request that changes something needs the token header and `application/json` (`ApiGroupTests.A_JSON_body_without_a_token_is_refused`, `A_type_other_than_application_json_is_refused_with_an_empty_body_too`). |
| 3.5.3 | 1 | HTTP methods for sensitive functions | Met: everything that changes state is POST, PUT or DELETE behind the token; the two export GETs only read and write an audit row, and SameSite=Strict keeps another site from sending the cookie. |
| 3.5.4 | 2 | Separate applications on separate hosts | Met: one application on one host name (design.md §7.1). |
| 3.5.5 | 2 | Messages received by postMessage | N/A: the SPA uses no `postMessage` (read). |
| 3.7.1 | 2 | Client technologies | Met: HTML, CSS and TypeScript only; no plug-ins or applets. |
| 3.7.2 | 2 | Redirects to allowed places | Met: the server redirects nowhere (the cookie events answer 401 and 403, `Program.cs`; HTTP to HTTPS is App Service's `httpsOnly`) and the SPA navigates only to its own routes. |

### V4 API and web service

| ID | L | Topic | Status |
|---|---|---|---|
| 4.1.1 | 1 | Content-Type matches the content | Met: JSON and problem+json from the framework, PDF and Word with their types (`ReportFile.cs`), PNG for the QR code, HTML for the page. |
| 4.1.2 | 2 | HTTP redirected to HTTPS | Met: `httpsOnly: true` in `infra/modules/app.bicep`, deployed from the file on every push; the only client of `/api` is the SPA in a browser, so no script is silently redirected. |
| 4.1.3 | 2 | Intermediary headers not settable by users | Met: `X-Forwarded-For` and `X-Forwarded-Proto` are taken only from the front end's networks, with `ForwardLimit` 1 (`ForwardedHeadersTests.An_address_a_client_put_before_the_one_the_front_end_added_is_ignored`); the app reads no user-ID header (`X-Test-Role` exists only in `tests/`). |
| 4.2.1 | 2 | Message length and request smuggling | Met by platform: Kestrel behind App Service's front end, with no custom parser or proxy in the app. |
| 4.3.1 | 2 | GraphQL query limits | N/A: the app has no GraphQL. |
| 4.3.2 | 2 | GraphQL introspection | N/A: the app has no GraphQL. |
| 4.4.1 | 1 | WebSocket over TLS | N/A: the app has no WebSockets. |
| 4.4.2 | 2 | WebSocket origin check | N/A: the app has no WebSockets. |
| 4.4.3 | 2 | WebSocket session management | N/A: the app has no WebSockets. |
| 4.4.4 | 2 | WebSocket session tokens | N/A: the app has no WebSockets. |

### V5 File handling

| ID | L | Topic | Status |
|---|---|---|---|
| 5.1.1 | 2 | Documented upload rules | N/A: the app accepts no file upload (the API reads JSON only, `ApiGroup.cs`). |
| 5.2.1 | 1 | Upload size limits | N/A: the app accepts no file upload. |
| 5.2.2 | 1 | Upload type checks | N/A: the app accepts no file upload. |
| 5.2.3 | 2 | Compressed uploads | N/A: the app accepts no file upload. |
| 5.3.1 | 1 | Stored files not executed | N/A: the app stores no file; the exports are made in memory and sent. |
| 5.3.2 | 1 | File paths from untrusted input | N/A: the app builds no file path from request data (the PDF font is an embedded resource). |
| 5.4.1 | 2 | Download file names | Met: the name is built from a validated date and fixed words (`daily-notes_`, `participant-record_`) in `DailyReportEndpoint.cs` and `ParticipantExportEndpoint.cs`; no user text is in it. |
| 5.4.2 | 2 | Served names encoded | Met: the same names, written into `Content-Disposition` by `TypedResults.File`. |
| 5.4.3 | 2 | Scanning files from untrusted sources | N/A: the app serves only files it generated. |

### V6 Authentication

| ID | L | Topic | Status |
|---|---|---|---|
| 6.1.1 | 1 | Documented defences against guessing and lockout abuse | Met: design.md §8, §9.9, A25, A55, A56, A95 and A96, and the inputs table of this record. |
| 6.1.2 | 2 | List of context-specific words | F-3: the app keeps no such list. |
| 6.1.3 | 2 | Sign-in paths documented and consistent | Met: design.md §8.1 and §8.2 give the two paths, a passkey and a password with an authenticator code; both end in a verified second factor (`Grow2NotesSignInManagerTests.The_check_before_signing_in_fails_a_password_user_without_two_factor`). |
| 6.2.1 | 1 | Password length | Met: at least 12 characters (`Limits.PasswordMinLength`, `Program.cs`; `SetupPasswordEndpointTests`). |
| 6.2.2 | 1 | Users can change their password | F-4: there is no password change; recovery goes through a manager. |
| 6.2.3 | 1 | Password change needs the current password | N/A: there is no password change (F-4). |
| 6.2.4 | 1 | Common passwords refused | F-3: no list of common or breached passwords is checked. |
| 6.2.5 | 1 | No composition rules | Met: Identity's composition rules are off (`Program.cs`, A22). |
| 6.2.6 | 1 | Password fields masked | Met: the password fields are `type=password`, which the Create password view lets the person show for a moment (`PasswordInput.tsx`). |
| 6.2.7 | 1 | Paste and password managers allowed | Met: no paste handler in the SPA, and the fields carry `new-password` and `current-password` hints (`PasswordInput.tsx`). |
| 6.2.8 | 1 | Password checked as received | Met: Identity's hasher checks the exact string; the app trims and changes nothing (`PasswordSignInEndpoint.cs`). |
| 6.2.9 | 2 | Long passwords allowed | Met: the app sets no maximum length (`Program.cs`). |
| 6.2.10 | 2 | No forced rotation | Met: the app never expires a password. |
| 6.2.11 | 2 | Context-specific words refused | F-3: the app keeps no such list. |
| 6.2.12 | 2 | Breached passwords refused | F-3: no breached-password check. |
| 6.3.1 | 1 | Defences against stuffing and brute force | Met: lockout of 5 in 15 minutes, the account limit of 10 a minute and the address limit of 30 (`PasswordSignInLimitsTests.Five_wrong_passwords_lock_sign_in_for_15_minutes_and_the_right_password_then_gets_a_401`, `RateLimitsTests.The_password_step_allows_10_a_minute_for_each_account_by_its_normalised_email`, `Sign_in_and_setup_endpoints_share_30_a_minute_for_each_client_address`); the denial they allow is F-14. |
| 6.3.2 | 1 | No default accounts | Met: no account is seeded; the first manager is made by `admin bootstrap` (`BootstrapCommandTests`). |
| 6.3.3 | 2 | Second factor required | Met: every account has a passkey with user verification or a password plus an authenticator code, and a password alone never signs in (`SetupPasswordEndpointTests.After_completion_a_password_sign_in_requires_the_code_and_starts_no_session`, `Grow2NotesSignInManagerTests`). |
| 6.3.4 | 2 | Every path equally strong | Met: no path skips the second factor; there is no remember-me, forgot-password or bypass code (design.md §8, D23). |
| 6.4.1 | 1 | Activation links | Met: no initial password is issued; the setup link holds a protected token, valid 7 days and ended by the security stamp when setup completes (`SetupTokenProviderTests`, `SetupPasswordEndpointTests.After_completion_the_same_link_gets_410_and_a_second_completion_401`); the 7 days are F-18. |
| 6.4.2 | 1 | No hints or secret questions | Met: none exist. |
| 6.4.3 | 2 | Reset does not bypass the second factor | Met: a manager's reset removes the password and passkeys and turns the authenticator off, and the person enrols a second factor again before signing in (`SignInResetTests.After_a_reset_the_worker_sets_up_again_with_a_password_and_a_code_and_signs_in_with_them`). |
| 6.4.4 | 2 | Lost factor needs proof of identity | Met: the person asks a manager by phone or in person (design.md §8.6, D74); there is no self-service route. |
| 6.5.1 | 2 | One-time codes usable once | F-2: a TOTP code can be used again inside its window; the emailed setup link is single use. |
| 6.5.2 | 2 | Stored lookup secrets and codes | N/A: the app issues no lookup secrets, recovery codes or out-of-band codes. |
| 6.5.3 | 2 | Random generation of such codes | N/A: the app issues none. |
| 6.5.4 | 2 | Strength of such codes | N/A: the app issues none. |
| 6.5.5 | 2 | Lifetime of out-of-band requests and codes | N/A: the app sends no authentication code or push request; the setup link enrols an account and is F-18. |
| 6.6.1 | 2 | Telephone and SMS methods | Met: only passkeys and an authenticator app are offered; there is no SMS or voice method. |
| 6.6.2 | 2 | Out-of-band requests bound to one sign-in | N/A: the app has no out-of-band authentication request. |
| 6.6.3 | 2 | Rate limit on out-of-band codes | N/A: the app has no out-of-band code; the code step is limited to 10 a minute for each account anyway. |
| 6.8.1 | 2 | Identity-provider user identifiers | N/A: Release 1 has no identity provider; sign-in is the app's own. |
| 6.8.2 | 2 | Identity-provider assertions signed | N/A: Release 1 has no identity provider. |
| 6.8.3 | 2 | Identity-provider replay | N/A: Release 1 has no identity provider. |
| 6.8.4 | 2 | Identity-provider authentication strength | N/A: Release 1 has no identity provider. |

### V7 Session management

| ID | L | Topic | Status |
|---|---|---|---|
| 7.1.1 | 2 | Documented timeouts | Met: design.md §8 and §8.5 (A24): 30 minutes idle, 12 hours absolute. |
| 7.1.2 | 2 | Documented concurrent sessions | Met: documented here: a person may be signed in on several devices at once with no cap, one cookie for each; signing out on any of them, a reset or a deactivation ends all (design.md A99; `LogoutEndpointTests.Every_other_session_of_the_person_gets_401_on_its_next_request`). |
| 7.1.3 | 2 | Documented re-authentication conditions | Met: design.md §8.5 and §8.6: idle and absolute limits, signing out on another device (A99), and deactivation or reset; there is no federation. |
| 7.2.1 | 1 | Session checked on the server | Met: the cookie is validated on every request and the user's row is read each time (`Program.cs`, `ValidationInterval` zero; `SessionRules.cs`). |
| 7.2.2 | 1 | Dynamically generated session tokens | Met: the session is a protected cookie ticket made at sign-in; the one static key, Resend's, is read from Key Vault and is not a session token. |
| 7.2.3 | 1 | Reference token strength | N/A: the session is a self-contained protected ticket, not a reference token (see chapter 9). |
| 7.2.4 | 1 | New token at sign-in | Met: every sign-in issues a new cookie (`Grow2NotesSignInManager.cs`), and no session cookie exists before it. |
| 7.3.1 | 2 | Inactivity timeout | Met by design, confirmed in test by step 11: 30 minutes (`SessionLimitTests.A_request_after_30_minutes_and_1_second_idle_gets_401`). |
| 7.3.2 | 2 | Absolute lifetime | Met: 12 hours (`SessionLimitTests.A_request_every_20_minutes_is_let_in_at_12_hours_and_refused_at_12_hours_and_1_minute`). |
| 7.4.1 | 1 | Sign-out ends the session for good | Met (F-1, fixed): sign-out rotates the security stamp, so the session, and any copy of its cookie, gets 401 on its next request (`LogoutEndpointTests.A_copy_of_the_session_cookie_gets_401_after_sign_out`). |
| 7.4.2 | 1 | Disabled accounts lose their sessions | Met: deactivation and reset rotate the security stamp, so each device gets 401 next request (`UserDeactivationEndpointsTests.Each_device_of_a_deactivated_worker_gets_401_on_its_next_request_and_their_passkey_too`, `SignInResetTests.Each_device_the_worker_is_signed_in_on_gets_401_on_its_next_request`). |
| 7.4.3 | 2 | Sessions ended after a factor changes | Met: only a manager's reset changes a person's factors and it ends every session (`SignInResetTests`); people change none themselves (F-4). |
| 7.4.4 | 2 | Sign-out on every signed-in page | Met by design, confirmed in test by step 7: the signed-in header has an Account menu with Sign out (steps 7 and 10). |
| 7.4.5 | 2 | Administrators can end sessions | Met: Deactivate and Reset sign-in in the app, `admin signout-all` and `admin reset-signin` in the console (`SignOutAllCommandTests`, `ResetSignInCommandTests`). |
| 7.5.1 | 2 | Re-authentication before sensitive changes | F-4: a manager changing a person's email or resetting their sign-in is not asked to sign in again. |
| 7.5.2 | 2 | People list and end their sessions | F-4: there is no list of sessions. |
| 7.6.1 | 2 | Federated session lifetimes | N/A: Release 1 has no federation. |
| 7.6.2 | 2 | Federated session creation | N/A: Release 1 has no federation. |

### V8 Authorization

| ID | L | Topic | Status |
|---|---|---|---|
| 8.1.1 | 1 | Documented access rules | Met: design.md §2 and §9.1; `EndpointMatrix.cs` holds a row for each endpoint. |
| 8.1.2 | 2 | Documented field-level rules | Met: design.md §2 and §6; the only fields that differ by role are the to-review count in `/api/auth/me` (`MeEndpoint.cs`) and the manager-only endpoints. |
| 8.2.1 | 1 | Function-level access | Met: deny by default; the fallback policy admits a Worker or Manager and `Manager` guards `/api/admin` and the manager-only routes (`Policies.cs`; `PoliciesTests`; `EndpointMatrixTests.Every_endpoint_answers_the_caller_with_the_status_in_its_row`; `SignedOutTests`). |
| 8.2.2 | 1 | Data-specific access | Met: a tenant query filter and save interceptor (`ModelTenancyTests`, `TenantIsolationOnNewEntitiesTests`); drafts are bound to their author (`NoteDrafts.cs`); another organisation's record answers 404 (design.md §9.2); everyone in an organisation may read its notes by design (D20). |
| 8.2.3 | 2 | Field-level access | Met: request bodies are fixed record types holding only the permitted fields, such as `UserDetailsRequest` and `DraftSaveRequest`; role, status and organisation are never bound from a body. |
| 8.3.1 | 1 | Rules enforced on the server | Met: policies and handlers run in the API, and the SPA hides nothing that the API relies on (`EndpointMatrixTests`). |
| 8.4.1 | 2 | Cross-tenant controls | Met: `ModelTenancyTests`, `TenantIsolationOnNewEntitiesTests`, composite foreign keys, and `IgnoreQueryFiltersSourceTests.No_source_file_outside_the_operator_commands_uses_IgnoreQueryFilters`. |

### V9 Self-contained tokens

| ID | L | Topic | Status |
|---|---|---|---|
| 9.1.1 | 1 | Token integrity checked | Met: the session cookie and the setup token are Data Protection payloads, which are encrypted and authenticated, and a changed one fails to unprotect (`SetupTokenProviderTests.A_setup_token_fails_for_another_user`); the app issues no JWTs. |
| 9.1.2 | 1 | Algorithm allowlist | Met: Data Protection's algorithms are fixed by its key ring; a token cannot name its own. |
| 9.1.3 | 1 | Key material from trusted sources | Met: the key ring is in the app's database, wrapped by a Key Vault key (`KeyRingWrapping.cs`, `KeyRingWrappingTests`, `infra/modules/keyvault.bicep`). |
| 9.2.1 | 1 | Validity time checked | Met: the cookie and the setup token carry an expiry that the server enforces (`SessionLimitTests`, `SetupStartEndpointTests.A_token_older_than_the_lifespan_gets_410`). |
| 9.2.2 | 2 | Token purpose checked | Met: a setup token verifies only for its provider and the Setup purpose (`SetupTokenProvider.cs`, `SetupTokenProviderTests`). |
| 9.2.3 | 2 | Token audience | N/A: one service makes and reads its tokens; none goes to another service. |
| 9.2.4 | 2 | Issuer and audience impersonation | N/A: one service makes and reads its tokens. |

### V10 OAuth and OIDC

| ID | L | Topic | Status |
|---|---|---|---|
| 10.1.1 | 2 | Tokens go only where needed | N/A: Release 1 is not an OAuth or OIDC client, resource server or authorisation server; Release 2's MCP server (T38) is reviewed when it is built. |
| 10.1.2 | 2 | Client accepts only its own transaction's values | N/A: Release 1 has no OAuth or OIDC (see 10.1.1). |
| 10.2.1 | 2 | Code flow protects against CSRF | N/A: Release 1 has no OAuth or OIDC (see 10.1.1). |
| 10.2.2 | 2 | Choosing among authorisation servers | N/A: Release 1 has no OAuth or OIDC (see 10.1.1). |
| 10.3.1 | 2 | Resource server accepts tokens meant for it | N/A: Release 1 has no OAuth or OIDC (see 10.1.1). |
| 10.3.2 | 2 | Decisions use token scope | N/A: Release 1 has no OAuth or OIDC (see 10.1.1). |
| 10.3.3 | 2 | User identified by issuer and subject | N/A: Release 1 has no OAuth or OIDC (see 10.1.1). |
| 10.3.4 | 2 | Authentication strength claims | N/A: Release 1 has no OAuth or OIDC (see 10.1.1). |
| 10.4.1 | 1 | Redirect URIs matched exactly | N/A: Release 1 has no OAuth or OIDC (see 10.1.1). |
| 10.4.2 | 1 | Authorisation code single use | N/A: Release 1 has no OAuth or OIDC (see 10.1.1). |
| 10.4.3 | 1 | Authorisation code short-lived | N/A: Release 1 has no OAuth or OIDC (see 10.1.1). |
| 10.4.4 | 1 | Grant types allowed to a client | N/A: Release 1 has no OAuth or OIDC (see 10.1.1). |
| 10.4.5 | 1 | Refresh token replay | N/A: Release 1 has no OAuth or OIDC (see 10.1.1). |
| 10.4.6 | 2 | Proof key for the code grant | N/A: Release 1 has no OAuth or OIDC (see 10.1.1). |
| 10.4.7 | 2 | Unregistered client consent | N/A: Release 1 has no OAuth or OIDC (see 10.1.1). |
| 10.4.8 | 2 | Refresh token expiry | N/A: Release 1 has no OAuth or OIDC (see 10.1.1). |
| 10.4.9 | 2 | Revoking tokens | N/A: Release 1 has no OAuth or OIDC (see 10.1.1). |
| 10.4.10 | 2 | Confidential client authentication | N/A: Release 1 has no OAuth or OIDC (see 10.1.1). |
| 10.4.11 | 2 | Scopes given to clients | N/A: Release 1 has no OAuth or OIDC (see 10.1.1). |
| 10.5.1 | 2 | ID token replay | N/A: Release 1 has no OAuth or OIDC (see 10.1.1). |
| 10.5.2 | 2 | Users identified by issuer and subject | N/A: Release 1 has no OAuth or OIDC (see 10.1.1). |
| 10.5.3 | 2 | Issuer mix-up | N/A: Release 1 has no OAuth or OIDC (see 10.1.1). |
| 10.5.4 | 2 | ID token audience | N/A: Release 1 has no OAuth or OIDC (see 10.1.1). |
| 10.5.5 | 2 | Back-channel logout tokens | N/A: Release 1 has no OAuth or OIDC (see 10.1.1). |
| 10.6.1 | 2 | Flows an OpenID Provider allows | N/A: Release 1 has no OAuth or OIDC (see 10.1.1). |
| 10.6.2 | 2 | Denial of service through authorisation requests | N/A: Release 1 has no OAuth or OIDC (see 10.1.1). |
| 10.7.1 | 2 | Consent prompt | N/A: Release 1 has no OAuth or OIDC (see 10.1.1). |
| 10.7.2 | 2 | Consent details shown | N/A: Release 1 has no OAuth or OIDC (see 10.1.1). |
| 10.7.3 | 2 | Reviewing and revoking consent | N/A: Release 1 has no OAuth or OIDC (see 10.1.1). |

### V11 Cryptography

| ID | L | Topic | Status |
|---|---|---|---|
| 11.1.1 | 2 | Key management policy | Met: design.md §9.3 and §9.4 name each key, who may use it and how it is protected; the key ring is in SQL and so in the backups (§10.7). |
| 11.1.2 | 2 | Cryptographic inventory | Met: the cryptographic inventory in section 4 of this record lists every key, algorithm and certificate. |
| 11.2.1 | 2 | Industry-validated implementations | Met: .NET's cryptography, ASP.NET Core Data Protection, Identity's hasher and the Azure SDK; the app implements no algorithm. |
| 11.2.2 | 2 | Changing algorithms | Met: each algorithm is a library setting in one place (Data Protection, `PasswordHasherOptions`, the Key Vault key), not spread through the code. |
| 11.2.3 | 2 | 128 bits of security | F-7: the Key Vault wrapping key is RSA 2048; the rest meets it (AES-256, HMAC-SHA256, TLS 1.2 or later). |
| 11.3.1 | 1 | No weak block modes or padding | Met: Data Protection uses AES-CBC with HMAC and the Key Vault client wraps with RSA-OAEP (the library names `RsaOaep` only); the app chooses no mode. |
| 11.3.2 | 1 | Approved ciphers and modes | Met: AES-256-CBC with HMAC-SHA256 (Data Protection's default) and RSA-OAEP for wrapping. |
| 11.3.3 | 2 | Encrypted data protected from change | Met: Data Protection encrypts then authenticates, so a changed payload fails to unprotect; data in SQL is not encrypted by the app, deliberately (design.md §9.3). |
| 11.4.1 | 1 | Approved hash functions | Met: only the SHA-2 family in the app and its libraries, except RFC 6238's HMAC-SHA1 inside Identity's authenticator check, which authenticator apps require; no MD5 or SHA-1 in `src/`. |
| 11.4.2 | 2 | Password storage | F-5: Identity's PBKDF2 at its default of 100,000 iterations. |
| 11.4.3 | 2 | Hashes in digital signatures | N/A: the app makes no digital signatures. |
| 11.4.4 | 2 | Keys derived from passwords | N/A: no key is derived from a password; passwords are only hashed for checking (11.4.2). |
| 11.5.1 | 2 | Random values | Met: secrets come from Identity and Data Protection; there is no `System.Random` in `src/`. |
| 11.6.1 | 2 | Public-key algorithms | Met: the one key pair is the Key Vault RSA key of 2048 bits, used with OAEP; TLS keys are the platform's (strength is F-7). |

### V12 Secure communication

| ID | L | Topic | Status |
|---|---|---|---|
| 12.1.1 | 1 | Current TLS versions | Met: `minTlsVersion` 1.2 on the web app and its publishing site and `minimalTlsVersion` 1.2 on SQL (`infra/modules/app.bicep`, `infra/modules/sql.bicep`), deployed from the files on every push; HSTS keeps browsers on HTTPS. |
| 12.1.2 | 2 | Cipher suites with forward secrecy | F-24: the suite list is Azure's platform default and not set in the repository. |
| 12.1.3 | 2 | Client certificates | N/A: the app uses no client certificates. |
| 12.2.1 | 1 | TLS for all external connections | Met: `httpsOnly`, HSTS with `includeSubDomains`; the app has no plain-HTTP listener of its own beyond App Service's front end. |
| 12.2.2 | 1 | Publicly trusted certificates | Met: both environments use App Service's own certificate on their default hosts (design.md §9.3, D62, D91). |
| 12.3.1 | 2 | TLS between services | Met: SQL with `Encrypt=Strict` (`infra/main.bicep`); Key Vault, Resend and Application Insights over HTTPS. |
| 12.3.2 | 2 | Certificates validated | Met: no `TrustServerCertificate` or certificate bypass in `src/`; CI's throwaway SQL container uses it alone (F-9). |
| 12.3.3 | 2 | Internal connections encrypted | Met by platform: the one hop without TLS is App Service's front end to the app, inside the platform; the app's own connections are all TLS. |
| 12.3.4 | 2 | Internal certificate authorities | N/A: the app uses no internal CA or self-signed certificate. |

### V13 Configuration

| ID | L | Topic | Status |
|---|---|---|---|
| 13.1.1 | 2 | Documented communication needs | Met: design.md §7.1, §7.7 and §9.4 list what the app talks to: SQL, Key Vault, Resend and Application Insights. |
| 13.2.1 | 2 | Authenticated backend communication | Met: managed identities for SQL, Key Vault and telemetry, and a bearer key read from Key Vault for Resend (design.md §9.4; `infra/main.bicep` uses `Active Directory Managed Identity`); no shared or password accounts. |
| 13.2.2 | 2 | Least privilege for service accounts | Met: the SQL role and the two Key Vault roles are narrow (`RuntimeRoleTests`); F-8, the broad email role, is fixed. |
| 13.2.3 | 2 | No default credentials | F-9: CI holds a throwaway SQL password; no deployed credential exists. |
| 13.2.4 | 2 | Allowlist of external resources | Met: the app calls only its configured endpoints and fetches no user-supplied address (the deploy workflow's own call is F-10). |
| 13.2.5 | 2 | Remote resources the server loads | N/A: the server loads no remote resources, and the CSP stops the page loading any (`connect-src 'self'`, `script-src 'self'`). |
| 13.3.1 | 2 | Secrets management | Met: Key Vault holds the one key and one secret, the Resend key, which each deploy writes from a GitHub environment secret as a secure parameter that stays out of deployment history (D94; `InfraTemplateTests`); both environments take deployments from `main` only, because any workflow run that enters an environment can read its secrets; the app settings hold IDs, addresses and the secret's URI only (`infra/main.bicep`); the GitHub subject prefix is passed when `infra/bootstrap.bicep` is run, not kept in the repository (F-25, fixed). |
| 13.3.2 | 2 | Least privilege to secrets | Met: RBAC on the vault; the app identity has Key Vault Crypto Service Encryption User and Key Vault Secrets User only; soft delete and purge protection are on (`infra/modules/bootstrap-environment.bicep`, `infra/modules/keyvault.bicep`). |
| 13.4.1 | 1 | No source-control data deployed | Met: `app.zip` is the output of `dotnet publish` (`ci.yml`), which holds no `.git` folder; `wwwroot` comes from the Vite build. |
| 13.4.2 | 2 | Debug modes off | Met: no setting turns on the development environment (Production is the default; `infra/main.bicep` sets none); no `EnableSensitiveDataLogging` or `EnableDetailedErrors` in `src/`; errors carry no exception text (`DatabaseFailureHandlerTests`, `ErrorCodeTests`). |
| 13.4.3 | 2 | No directory listing | Met: no directory browsing is enabled; static files come from `MapStaticAssets`. |
| 13.4.4 | 2 | HTTP TRACE | Met: no endpoint accepts TRACE, and the any-method catch-alls answer 404 (`Program.cs`). |
| 13.4.5 | 2 | Documentation and monitoring endpoints | Met: no Swagger, OpenAPI or management endpoint in `src/`; `/healthz` and `/healthz/ready` answer a status word only (`HealthEndpoints.cs`). |

### V14 Data protection

| ID | L | Topic | Status |
|---|---|---|---|
| 14.1.1 | 2 | Sensitive data classified | Met: design.md §9 and §12 treat participant notes as health information. |
| 14.1.2 | 2 | Protection for each class | Met: design.md §9.3, §9.5 and §9.6, evidenced in section 4 of this record (see F-12 for the authenticator key). |
| 14.2.1 | 1 | No sensitive data in URLs | Met: URLs hold IDs and dates only; the setup token is in the fragment and is removed at once (design.md §8.1, `SetupPage.tsx`). |
| 14.2.2 | 2 | Not cached by intermediaries | Met: `Cache-Control: no-store` on every `/api` response (`CacheHeaders.cs`, `CacheHeadersTests`). |
| 14.2.3 | 2 | Not sent to untrusted parties | Met: the SPA talks to its own origin only (`connect-src 'self'`) and `npm run check:no-analytics` runs over the build in CI. |
| 14.2.4 | 2 | Controls for each class of data | Met: encryption in transit and at rest, access control, audit and retention (design.md §9.3, §5.4, `ops/retention-and-destruction.md`). |
| 14.3.1 | 1 | Data cleared when the session ends | Met by design, confirmed in test by steps 7 and 61: sign-out sends `Clear-Site-Data` (`LogoutEndpointTests.Answers_204_with_Clear_Site_Data_and_no_store_and_deletes_the_session_cookie`) and the SPA keeps data in memory only. |
| 14.3.2 | 2 | Anti-caching headers | Met: `no-store` under `/api` and `no-cache` for `index.html` (`CacheHeaders.cs`, `CacheHeadersTests`). |
| 14.3.3 | 2 | Nothing sensitive in browser storage | Met by design, confirmed in test by step 61: the SPA uses no `localStorage`, `sessionStorage` or IndexedDB (read). |

### V15 Secure coding and architecture

| ID | L | Topic | Status |
|---|---|---|---|
| 15.1.1 | 1 | Remediation time frames for vulnerable components | Met: design.md §9.11 and this record: a high or critical advisory fails the build and is fixed before merge; other advisories arrive in the weekly grouped Dependabot update. |
| 15.1.2 | 2 | Inventory of third-party components | Met: `Directory.Packages.props`, the two `package-lock.json` files and GitHub's dependency graph built from them; no SBOM file is made. |
| 15.1.3 | 2 | Documented heavy functionality | Met: the exports are the heavy functions (design.md §11.5, §11.6; `DailyReportTimingTests`, `ParticipantExportTimingTests`) and have their own limit (A96). |
| 15.2.1 | 1 | No component past its remediation time | Met: NuGet Audit (`Directory.Build.props`), `npm audit` at high (`ci.yml`) and Dependabot (`.github/dependabot.yml`); F-22 lists what Dependabot does not cover. |
| 15.2.2 | 2 | Defences against heavy requests | Met: the export limit (`RateLimitsTests.Daily_report_and_record_exports_share_10_a_minute_for_each_user`), Kestrel's default request limits and a 5-second health-check timeout; the unlimited readiness check is F-11. |
| 15.2.3 | 2 | Nothing extra in production | Met: the deployed package is the web project's publish output; test-only code (`TestSignIn`, `X-Test-Role`, `TestOnlyApiEndpoint`) lives in `tests/`. |
| 15.3.1 | 1 | Only required data returned | Met: responses are purpose-built records such as `Me` and `MyDraft`, never entities (read). |
| 15.3.2 | 2 | Backend calls and redirects | N/A: the app makes no backend call to a user-supplied address. |
| 15.3.3 | 2 | Mass assignment | Met: see 8.2.3. |
| 15.3.4 | 2 | Proxies pass the real client address | Met: forwarded headers (`Program.cs`, `ForwardedHeadersTests`; step 142 checks it live). |
| 15.3.5 | 2 | Variable types | Met: strict C# and TypeScript; request records are typed and validated. |
| 15.3.6 | 2 | JavaScript prototype pollution | Met: the SPA does no deep merge and builds no objects from user-chosen keys (no `Object.assign` or merge helper in its source). |
| 15.3.7 | 2 | HTTP parameter pollution | Met: each endpoint binds a parameter to one typed value, parsed strictly (A94). |

### V16 Security logging and error handling

| ID | L | Topic | Status |
|---|---|---|---|
| 16.1.1 | 2 | Log inventory | Met: design.md §9.5 (telemetry) and §5.3 to §5.4 (audit events) are the inventory. |
| 16.2.1 | 2 | Log entry metadata | Met: telemetry has time, operation, route and status; an audit event has time, actor, organisation, type, entity and client address (`AuditWriter.cs`). |
| 16.2.2 | 2 | Time sources | Met: times come from `TimeProvider` and are stored as UTC (A33). |
| 16.2.3 | 2 | Logs held as documented | Met: Log Analytics for 30 days with a daily cap, and the audit table; no other sink (`infra/modules/monitoring.bicep`). |
| 16.2.4 | 2 | Common log format | Met: `ILogger` through OpenTelemetry to Application Insights. |
| 16.2.5 | 2 | Sensitive data kept out | Met: IDs only; EF command logs are off and SQL error text is stripped (`TelemetryCanaryTests.A_canary_in_request_bodies_and_SQL_error_text_appears_in_no_log_telemetry_or_response`, `PasswordSignInEndpointTests.No_log_entry_holds_the_email_or_the_password`, `TotpSignInEndpointTests.No_log_entry_holds_the_code`). |
| 16.3.1 | 2 | Authentication events logged | Met: sign-ins, failures, lockouts, setup and sign-out each write an audit event (`AuditEventsTests`, `PasswordSignInEndpointTests`, `TotpSignInEndpointTests`, `LogoutEndpointTests`); an unknown email or credential is counted by one log line (A56). |
| 16.3.2 | 2 | Failed authorization logged | Met: 401 and 403 answers appear as request telemetry with their status. |
| 16.3.3 | 2 | Validation, business-logic and anti-automation events | Met: 422, 404 and 429 answers appear as request telemetry with their status; a 429's key is not logged, by design (A95). |
| 16.3.4 | 2 | Unexpected errors logged | Met: a database failure is logged with its type and SQL number (`DatabaseFailureHandlerTests`); other exceptions reach telemetry as exceptions (`TelemetryTests`). |
| 16.4.1 | 2 | Log injection | Met: fixed message templates with structured values, and audit details written as JSON. |
| 16.4.2 | 2 | Logs protected | Met: audit rows are append-only for the app's role (`AuditEventAppendOnlyTests`, `RuntimeRoleTests`); Log Analytics reading is limited to the developer and local ingestion auth is off (`infra/modules/monitoring.bicep`). |
| 16.4.3 | 2 | Logs sent to a separate system | Met: to Application Insights over TLS as the app's managed identity (`TelemetryTests.With_a_connection_string_and_a_client_ID_telemetry_is_sent_as_the_app_identity`). |
| 16.5.1 | 2 | Generic error messages | Met: problem details with no exception text in every environment (`ErrorCodeTests`, `DatabaseFailureHandlerTests.A_database_failure_gets_a_500_problem_and_one_log_entry_with_the_type_and_SQL_error_number_only`). |
| 16.5.2 | 2 | Fail safely when a service fails | Met: a failed audit write fails the request and signs nobody in (`SetupPasswordEndpointTests.With_an_audit_writer_that_throws_nothing_commits_and_no_session_starts`), and a failed email changes nothing (`InviteUserEndpointTests.A_sender_that_fails_gives_a_500_with_no_user_or_event_and_no_address_or_link_in_the_logs`). |
| 16.5.3 | 2 | Fail gracefully | Met: `UseExceptionHandler` and the two exception handlers (`Program.cs`, `BadHttpRequestHandler.cs`, `DatabaseFailureHandler.cs`). |

### V17 WebRTC

| ID | L | Topic | Status |
|---|---|---|---|
| 17.1.1 | 2 | TURN server address families | N/A: the app has no WebRTC. |
| 17.2.1 | 2 | DTLS key management | N/A: the app has no WebRTC. |
| 17.2.2 | 2 | DTLS-SRTP | N/A: the app has no WebRTC. |
| 17.2.3 | 2 | SRTP authentication | N/A: the app has no WebRTC. |
| 17.2.4 | 2 | Media server malformed packets | N/A: the app has no WebRTC. |
| 17.3.1 | 2 | Signalling rate limits | N/A: the app has no WebRTC. |
| 17.3.2 | 2 | Signalling malformed messages | N/A: the app has no WebRTC. |

## 6. ZAP baseline scan

Not yet run: `ops/first-use-in-test.md` step 141.

When it has been run, the operator writes here what was covered and decides each alert. Raw reports stay with the
operator, because they can hold cookie or token values. S06.01.02's criteria: the scan is of the test site, which
holds made-up data; no High alert is open; every other alert is fixed, accepted with a reason, or a false positive.

**Covered.** Targets, passes, number of URLs and number of `429` answers: not yet recorded.

| Alert ID | Name | Risk | Where | Decision |
|---|---|---|---|---|
