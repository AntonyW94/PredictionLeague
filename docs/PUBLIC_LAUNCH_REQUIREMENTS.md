# PredictionLeague - Public Launch Requirements

**Generated:** January 2026
**Purpose:** Comprehensive analysis of features required before launching to the public

---

## Executive Summary

This document outlines all features, fixes, and improvements required before PredictionLeague can be launched publicly. Items are grouped by priority based on legal requirements, security risks, and user experience impact.

### Priority Definitions

| Priority | Definition |
|----------|------------|
| 🔴 **Critical** | Must have before launch - legal/security requirements or showstoppers |
| 🟠 **High** | Should have before launch - significant user experience or security gaps |
| 🟡 **Medium** | Nice to have at launch - improves experience but not blocking |
| 🟢 **Low** | Post-launch improvements - can be added iteratively |

### Quick Stats

| Category | Critical | High | Medium | Low |
|----------|----------|------|--------|-----|
| Legal & Compliance | 7 | 3 | 2 | 0 |
| Security | 5 | 6 | 4 | 2 |
| Authentication | 4 | 3 | 2 | 1 |
| Email & Notifications | 2 | 3 | 3 | 2 |
| User Experience | 3 | 4 | 5 | 4 |
| Admin & Moderation | 3 | 4 | 3 | 2 |
| Error Handling | 2 | 4 | 3 | 2 |
| Data & Scalability | 1 | 5 | 3 | 2 |

---

## 🔴 Critical Priority (Must Have Before Launch)

### Legal & Compliance

#### 1. Privacy Policy Page
- **Status:** ❌ Missing
- **Requirement:** GDPR, UK Data Protection Act 2018
- **Details:** No `/privacy` route, no privacy policy content, no footer link
- **Action:** Create Privacy Policy page explaining data collection, storage, sharing, and user rights
- **Effort:** Medium (content + legal review needed)

#### 2. Terms of Service Page
- **Status:** ❌ Missing
- **Requirement:** Legal protection for the business
- **Details:** No `/terms` route, no terms content
- **Action:** Create Terms of Service covering acceptable use, liability limitations, account termination
- **Effort:** Medium (content + legal review needed)

#### 3. Cookie Consent Banner
- **Status:** ❌ Missing
- **Requirement:** GDPR, UK PECR regulations
- **Details:** `app.UseCookiePolicy()` exists but no consent UI, no cookie categories
- **Action:** Implement cookie consent banner with accept/reject options and preference management
- **Effort:** Medium

#### 4. Registration Legal Checkboxes
- **Status:** ❌ Missing
- **Details:** Register page has no "I agree to Terms" or "I agree to Privacy Policy" checkboxes
- **Action:** Add required checkboxes and track consent timestamp in database
- **Effort:** Low

#### 5. Age Verification on Signup
- **Status:** ❌ Missing
- **Requirement:** If operating as gambling/gaming site, UK requires 18+ verification
- **Details:** No age field, no "I am 18+" checkbox, no date of birth collection
- **Action:** Add age confirmation checkbox at minimum; consider full DOB for stricter compliance
- **Effort:** Low-Medium

#### 6. Gambling/Gaming Disclaimers
- **Status:** ❌ Missing
- **Requirement:** UK Gambling Commission guidelines (if applicable)
- **Details:** Site involves predictions with prizes/money but has no responsible gaming warnings
- **Action:** Add disclaimers on homepage and prediction pages; include gambling help resources
- **Effort:** Low

#### 7. GDPR Data Subject Rights
- **Status:** ❌ Missing
- **Requirement:** GDPR Articles 15-22
- **Details:**
  - No user data export feature (Right of Access)
  - No self-service account deletion (Right to Erasure)
  - Admin can delete users, but users cannot delete themselves
- **Action:** Implement `/api/account/export-data` and `/api/account/delete` endpoints with UI
- **Effort:** High

### Security

#### 8. Rate Limiting
- **Status:** ❌ Missing
- **Risk:** High - vulnerable to brute force, credential stuffing, DDoS
- **Details:** No rate limiting on any API endpoints including login
- **Action:** Implement rate limiting middleware (e.g., AspNetCoreRateLimit) with limits per endpoint/IP
- **Effort:** Medium

#### 9. Account Lockout After Failed Login
- **Status:** ❌ Missing (database fields exist but unused)
- **Risk:** High - brute force vulnerability
- **Details:** `LockoutEnabled`, `LockoutEnd`, `AccessFailedCount` columns exist but not used
- **Action:** Configure ASP.NET Identity lockout options (e.g., 5 attempts = 15 min lockout)
- **Effort:** Low

#### 10. Security Headers
- **Status:** ❌ Missing
- **Risk:** Medium-High
- **Missing headers:**
  - Content-Security-Policy (CSP)
  - X-Frame-Options
  - X-Content-Type-Options
  - X-XSS-Protection
  - Referrer-Policy
  - Permissions-Policy
- **Action:** Add security headers middleware
- **Effort:** Low

#### 11. Input Validation - Password Complexity
- **Status:** ⚠️ Partial (only 8 char minimum)
- **Risk:** Medium - weak passwords allowed
- **Details:** No uppercase/lowercase/number/symbol requirements
- **Action:** Enhance `RegisterRequestValidator` with complexity rules
- **Effort:** Low

#### 12. Audit Logging
- **Status:** ❌ Missing
- **Risk:** High for compliance and incident response
- **Details:** No tracking of who did what and when (logins, data changes, admin actions)
- **Action:** Implement audit logging for authentication events and data modifications
- **Effort:** Medium-High

### Authentication

#### 13. Password Reset / Forgot Password
- **Status:** ❌ Missing
- **Impact:** Critical - users cannot recover accounts
- **Details:** No forgot password link, no reset flow, no email token
- **Action:** Implement `ForgotPasswordCommand`, `ResetPasswordCommand`, email template
- **Effort:** Medium

#### 14. Email Verification on Registration
- **Status:** ❌ Missing
- **Impact:** High - allows fake email registrations
- **Details:** Accounts usable immediately; `EmailConfirmed` field never set for email registrations
- **Action:** Send verification email on registration, require confirmation before full access
- **Effort:** Medium

#### 15. Change Password (Authenticated)
- **Status:** ❌ Missing
- **Impact:** High - compromised accounts cannot change password
- **Details:** No change password functionality after login
- **Action:** Implement `ChangePasswordCommand` with current password verification
- **Effort:** Low

#### 16. Account Deletion (Self-Service)
- **Status:** ❌ Missing
- **Impact:** Critical for GDPR compliance
- **Details:** Only admins can delete users; users cannot delete themselves
- **Action:** Add self-service account deletion with confirmation
- **Effort:** Medium

### User Experience

#### 17. Help Documentation / FAQ
- **Status:** ❌ Missing
- **Impact:** High support burden without documentation
- **Details:** No `/help`, `/faq`, or documentation pages
- **Action:** Create help pages explaining predictions, scoring, leagues, boosts
- **Effort:** Medium

#### 18. User Onboarding / Tutorial
- **Status:** ❌ Missing
- **Impact:** New users won't understand how to use the site
- **Details:** No guided tour, no getting started checklist, no contextual help
- **Action:** Create onboarding flow for first-time users
- **Effort:** Medium-High

#### 19. Contact / Support Page
- **Status:** ❌ Missing
- **Impact:** Users have no way to get help or report issues
- **Details:** No contact form, no support email, no about page
- **Action:** Create contact page with support email and/or form
- **Effort:** Low

### Admin & Moderation

#### 20. User Reporting System
- **Status:** ❌ Missing
- **Impact:** Cannot handle abuse or harassment reports
- **Details:** No mechanism for users to report inappropriate behaviour
- **Action:** Implement report functionality with admin review queue
- **Effort:** Medium-High

#### 21. User Suspension/Ban (Temporary)
- **Status:** ❌ Missing (only permanent deletion exists)
- **Impact:** Cannot moderate users without deleting them
- **Details:** No suspension, timeout, or soft-ban features
- **Action:** Implement user suspension with duration and reason
- **Effort:** Medium

#### 22. Admin Activity Logs
- **Status:** ❌ Missing
- **Impact:** No accountability for admin actions
- **Details:** Admin actions not tracked anywhere
- **Action:** Log all admin actions (user changes, role changes, deletions)
- **Effort:** Medium

### Error Handling

#### 23. Health Check Endpoints
- **Status:** ❌ Missing
- **Impact:** Cannot monitor application health
- **Details:** No `/health` or `/healthz` endpoints
- **Action:** Implement ASP.NET Core health checks for database, external APIs
- **Effort:** Low

#### 24. External API Resilience (Football API)
- **Status:** ❌ Missing
- **Impact:** High - site completely fails if API unavailable
- **Details:** No retry logic, no circuit breaker, no fallback data, failures are silent
- **Action:** Implement Polly retry policies and circuit breaker; add caching for fallback
- **Effort:** Medium-High

### Data & Scalability

#### 25. Caching Strategy
- **Status:** ❌ Missing
- **Impact:** High - all requests hit database
- **Details:** No Redis, no MemoryCache, no response caching
- **Action:** Implement caching for leaderboards, team lists, season data
- **Effort:** Medium-High

---

## 🟠 High Priority (Should Have Before Launch)

### Legal & Compliance

#### 26. Cookie Categories & Preferences
- **Details:** Beyond basic consent, allow users to manage cookie preferences (analytics, marketing)
- **Effort:** Medium

#### 27. Data Retention Policy Documentation
- **Details:** Document how long data is kept and when it's deleted
- **Effort:** Low

#### 28. Accessibility Statement
- **Details:** Page explaining accessibility features and contact for accessibility issues
- **Effort:** Low

### Security

#### 29. Two-Factor Authentication (2FA)
- **Status:** ❌ Missing (database fields exist)
- **Details:** No TOTP/authenticator support; `TwoFactorEnabled` field unused
- **Action:** Implement 2FA for enhanced account security
- **Effort:** High

#### 30. Session Management UI
- **Status:** ❌ Missing
- **Details:** Users can't see active sessions or logout from specific devices
- **Action:** Show active sessions, allow individual session revocation
- **Effort:** Medium

#### 31. Suspicious Activity Detection
- **Status:** ❌ Missing
- **Details:** No monitoring for multiple failed logins, unusual access patterns
- **Action:** Implement detection and alerting
- **Effort:** Medium-High

#### 32. Request Size Limits
- **Status:** ❌ Missing
- **Details:** Vulnerable to large payload attacks
- **Action:** Configure FormOptions to limit request sizes
- **Effort:** Low

#### 33. IP-Based Admin Protection
- **Status:** ❌ Missing
- **Details:** Admin endpoints accessible from any IP
- **Action:** Consider IP whitelist for admin endpoints
- **Effort:** Medium

#### 34. Sensitive Data Masking in Logs
- **Status:** ⚠️ Partial issue found
- **Details:** Refresh tokens may appear in error logs
- **Action:** Audit and mask sensitive data in all log statements
- **Effort:** Low

### Authentication

#### 35. Password History Prevention
- **Status:** ❌ Missing
- **Details:** Users can reuse the same password
- **Action:** Store password history hashes and prevent reuse
- **Effort:** Medium

#### 36. OAuth Provider Expansion
- **Status:** ⚠️ Only Google
- **Details:** Consider adding Apple Sign-In (required for iOS apps), Microsoft, Facebook
- **Effort:** Medium per provider

#### 37. Account Recovery Options
- **Status:** ❌ Missing
- **Details:** No secondary email or phone recovery options
- **Effort:** Medium

### Email & Notifications

#### 38. Email Unsubscribe Functionality
- **Status:** ❌ Missing
- **Requirement:** CAN-SPAM, GDPR
- **Details:** No unsubscribe links in emails, no email suppression
- **Action:** Add unsubscribe links and honour preferences
- **Effort:** Medium

#### 39. Welcome Email
- **Status:** ❌ Missing
- **Details:** Users get no email after registration
- **Action:** Send welcome email with getting started information
- **Effort:** Low

#### 40. Notification Preferences
- **Status:** ❌ Missing
- **Details:** Users cannot control what emails they receive
- **Action:** Add notification preferences page and respect settings
- **Effort:** Medium

### User Experience

#### 41. Accessibility Improvements (WCAG 2.1 AA)
- **Status:** ⚠️ Partial
- **Missing:**
  - Skip to main content link
  - High contrast mode option
  - Reduced motion support (prefers-reduced-motion)
  - Full keyboard navigation
  - Screen reader optimization
- **Effort:** Medium-High

#### 42. Search Functionality
- **Status:** ❌ Missing
- **Details:** No search for leagues, members, or leaderboards
- **Effort:** Medium

#### 43. Social Sharing Buttons
- **Status:** ❌ Missing (meta tags exist)
- **Details:** No share buttons for Facebook, Twitter, WhatsApp
- **Effort:** Low

#### 44. Skeleton Loading States
- **Status:** ❌ Missing (only text "Loading...")
- **Details:** No shimmer/skeleton placeholders
- **Effort:** Medium

### Admin & Moderation

#### 45. Admin Dashboard with Analytics
- **Status:** ❌ Missing
- **Details:** No overview of users, leagues, engagement metrics
- **Effort:** Medium-High

#### 46. Content Moderation Queue
- **Status:** ❌ Missing
- **Details:** No queue for reviewing reported content
- **Effort:** Medium

#### 47. Announcement/Broadcast System
- **Status:** ❌ Missing
- **Details:** No way to send global notifications or maintenance warnings
- **Effort:** Medium

#### 48. Bulk Operations
- **Status:** ❌ Missing
- **Details:** No bulk user management, import/export
- **Effort:** Medium

### Error Handling

#### 49. Circuit Breaker Pattern
- **Status:** ❌ Missing
- **Details:** External API failures can cascade; no Polly integration
- **Effort:** Medium

#### 50. Email Delivery Retry
- **Status:** ❌ Missing
- **Details:** Failed emails are logged but not retried
- **Effort:** Medium

#### 51. Alerting Configuration
- **Status:** ❌ Missing
- **Details:** Datadog integrated but no alert rules defined
- **Effort:** Low-Medium

#### 52. Correlation IDs / Distributed Tracing
- **Status:** ❌ Missing
- **Details:** No request correlation across services
- **Effort:** Medium

### Data & Scalability

#### 53. Pagination on List Endpoints
- **Status:** ❌ Missing
- **Details:** All queries return full datasets (GetMyLeagues, FetchAllTeams, etc.)
- **Effort:** Medium

#### 54. Database Connection Pooling Configuration
- **Status:** ⚠️ Using defaults
- **Details:** No explicit pool size configuration in connection string
- **Effort:** Low

#### 55. Response Caching Headers
- **Status:** ❌ Missing
- **Details:** No Cache-Control, ETag, or Last-Modified headers
- **Effort:** Low-Medium

#### 56. Database Connection Resilience
- **Status:** ❌ Missing
- **Details:** No retry policies for transient database failures
- **Effort:** Medium

#### 57. Request Timeout Configuration
- **Status:** ❌ Missing
- **Details:** No explicit timeouts on HttpClient or database queries
- **Effort:** Low

---

## 🟡 Medium Priority (Nice to Have at Launch)

### Legal & Compliance

#### 58. Acceptable Use Policy
- **Details:** Separate document explaining prohibited behaviours
- **Effort:** Low

#### 59. Competition/Prize Rules
- **Details:** Clear documentation of how prizes work and eligibility
- **Effort:** Low

### Security

#### 60. API Key Rotation Mechanism
- **Status:** ❌ Missing
- **Details:** Scheduler API key cannot be rotated without redeployment
- **Effort:** Medium

#### 61. Password Expiration Policy
- **Details:** Optional forced password changes after N days
- **Effort:** Medium

#### 62. Login Notification Emails
- **Details:** Email users when login from new device/location
- **Effort:** Medium

#### 63. Content-Type Validation
- **Details:** Enforce JSON Content-Type on API requests
- **Effort:** Low

### Authentication

#### 64. Remember Me / Extended Sessions
- **Details:** Option for longer refresh token validity
- **Effort:** Low

#### 65. Login History
- **Details:** Show users their recent login activity
- **Effort:** Medium

### Email & Notifications

#### 66. Email Delivery Tracking
- **Status:** ❌ Missing
- **Details:** No tracking of delivery, bounces, or complaints
- **Effort:** Medium

#### 67. Rich Email Templates
- **Details:** More notification types (results published, prizes awarded)
- **Effort:** Medium

#### 68. Push Notifications (Browser)
- **Status:** ❌ Missing
- **Details:** No service worker or push notification support
- **Effort:** High

### User Experience

#### 69. Dark/Light Theme Toggle
- **Status:** ❌ Missing (dark purple only)
- **Effort:** Medium-High

#### 70. Breadcrumb Navigation
- **Status:** ❌ Missing
- **Effort:** Low

#### 71. Print-Friendly Views
- **Status:** ❌ Missing
- **Details:** No print CSS for leaderboards
- **Effort:** Low

#### 72. Filtering and Sorting
- **Status:** ❌ Missing
- **Details:** No sortable columns in tables
- **Effort:** Medium

#### 73. User Preferences Panel
- **Details:** Centralized settings page
- **Effort:** Medium

### Admin & Moderation

#### 74. Feature Flags
- **Status:** ❌ Missing
- **Details:** No ability to toggle features without deployment
- **Effort:** Medium-High

#### 75. System Health Dashboard
- **Details:** Visual dashboard for monitoring
- **Effort:** Medium

#### 76. Support Ticket System
- **Details:** Integration with help desk or built-in ticketing
- **Effort:** High

### Error Handling

#### 77. Custom Error Pages (404, 500)
- **Status:** ⚠️ Basic only
- **Details:** Improve error pages with helpful information
- **Effort:** Low

#### 78. Client-Side Error Boundaries
- **Status:** ❌ Missing
- **Details:** Blazor ErrorBoundary components not used
- **Effort:** Medium

#### 79. Graceful Degradation
- **Details:** Show cached/fallback data when external APIs fail
- **Effort:** Medium-High

### Data & Scalability

#### 80. CDN for Static Assets
- **Status:** ❌ Missing
- **Details:** All static files served from web server
- **Effort:** Medium

#### 81. Database Migrations System
- **Status:** ❌ Missing
- **Details:** No version-controlled schema changes
- **Effort:** Medium-High

#### 82. Query Performance Monitoring
- **Details:** Track slow queries, missing indexes
- **Effort:** Medium

---

## 🟢 Low Priority (Post-Launch)

### Security

#### 83. Hardware Security Key Support (WebAuthn)
- **Details:** FIDO2 passwordless authentication
- **Effort:** High

#### 84. Penetration Testing
- **Details:** Professional security audit
- **Effort:** External

### Authentication

#### 85. Social Login Expansion
- **Details:** Apple, Microsoft, Facebook login options
- **Effort:** Medium per provider

### Email & Notifications

#### 86. SMS Notifications
- **Details:** Requires business registration for WhatsApp
- **Effort:** High

#### 87. In-App Notification Centre
- **Details:** Persistent notification history
- **Effort:** Medium-High

### User Experience

#### 88. Progressive Web App (PWA)
- **Status:** ❌ Missing
- **Details:** No service worker, manifest, or offline support
- **Effort:** High

#### 89. Localisation / Multi-Language
- **Status:** ❌ English only
- **Effort:** High

#### 90. Mobile Apps
- **Details:** iOS and Android native apps
- **Effort:** Very High

#### 91. Keyboard Shortcuts
- **Details:** Power user features
- **Effort:** Medium

### Admin & Moderation

#### 92. Advanced Analytics
- **Details:** User cohorts, funnels, engagement metrics
- **Effort:** High

#### 93. Automated Moderation
- **Details:** Auto-flag suspicious patterns
- **Effort:** High

### Error Handling

#### 94. Dead Letter Queue
- **Details:** Persist failed jobs for retry
- **Effort:** High

#### 95. APM Integration
- **Details:** Full Datadog APM (beyond logs)
- **Effort:** Medium

### Data & Scalability

#### 96. Data Archiving
- **Status:** ❌ Missing
- **Details:** Archive old seasons and predictions
- **Effort:** Medium

#### 97. Read Replicas
- **Details:** Database scaling (hosting dependent)
- **Effort:** High

---

## Implementation Roadmap Suggestion

### Phase 1: Legal & Security (Before Any Public Access)
1. Privacy Policy & Terms of Service pages
2. Cookie consent banner
3. Registration checkboxes (age, terms, privacy)
4. Rate limiting
5. Account lockout
6. Security headers
7. Password reset flow
8. Email verification

### Phase 2: Core User Features (Before Soft Launch)
1. Change password functionality
2. Self-service account deletion
3. Help/FAQ pages
4. Contact page
5. Unsubscribe functionality
6. Welcome email
7. Health check endpoints

### Phase 3: Enhanced Security & UX (Before Full Launch)
1. Audit logging
2. User reporting system
3. Admin dashboard basics
4. Caching implementation
5. Pagination
6. Accessibility improvements
7. User onboarding flow

### Phase 4: Polish & Scale (Post-Launch)
1. 2FA
2. Social sharing
3. Advanced notifications
4. Push notifications
5. Theme options
6. Performance optimisations

---

## What Already Works Well

The codebase has solid foundations:

✅ **Authentication:** JWT + refresh tokens properly implemented
✅ **Authorization:** Role-based access control working
✅ **Mobile Responsiveness:** Comprehensive mobile-first CSS
✅ **Loading States:** Good UX for async operations
✅ **Error Handling:** Global middleware with proper status codes
✅ **Logging:** Serilog + Datadog integration
✅ **SQL Security:** Parameterized queries throughout
✅ **Google OAuth:** Social login functional
✅ **HTTPS/HSTS:** Secure transport enforced
✅ **Input Validation:** FluentValidation + Guard clauses
✅ **CSS Architecture:** Well-organised design system
✅ **Admin Features:** Seasons, rounds, teams, users management

---

## Estimated Total Effort

| Priority | Item Count | Rough Effort |
|----------|------------|--------------|
| 🔴 Critical | 25 | 4-6 weeks |
| 🟠 High | 32 | 4-6 weeks |
| 🟡 Medium | 25 | 4-6 weeks |
| 🟢 Low | 15 | Ongoing |

**Recommended timeline:** 8-12 weeks for Critical + High priority items before public launch.

---

## Notes

- This analysis was generated by reviewing the codebase structure, not runtime behaviour
- Some items may already be partially implemented in ways not visible in code
- Legal requirements should be verified with a solicitor familiar with UK gambling/gaming law
- Security recommendations should be validated with penetration testing before launch
- Effort estimates are rough and depend on developer familiarity with the codebase
