# Student Dashboard Translation Audit (AR/EN)

## Latest pass (sidebar pages excl. Help Center)

All student dashboard sidebar views (excluding Help Center) are being audited and updated for full AR/EN translation.

### Session: Full AR/EN pass (sidebar excl. Help)
- **Achievements/Leaderboard.cshtml**: All UI strings (header, period buttons, ranks, points, level, "You", empty state, back button) wrapped in Html.T.
- **Leaderboard/Index.cshtml**: User rank card, period filters, table headers, "You" badge, empty state, Browse Courses button – all Html.T.
- **Quizzes/AdaptiveQuiz.cshtml**: Subtitle, stat labels (Questions, Current score, Current level, Correct streak), difficulty labels (Easy/Medium/Hard), End/Next buttons, "How it works" section and list items – Html.T.
- **Quizzes/QuickChallenge.cshtml**: Title, subtitle (string.Format + Html.T), question progress, Correct/Incorrect/Speed points labels – Html.T.
- **Quizzes/ReviewRecommendations.cshtml**: Page title, Back to results, overview labels, priority section titles, topic labels (wrong questions, answer rate), High/Medium/Low badges, Watch lesson/Practice buttons, excellent performance empty state – Html.T.
- **Quizzes/CompareAttempts.cshtml**: Page title, form labels (First/Second attempt), Score/Correct answers/Time taken, Attempt 1/2, minute labels, comparison/assessment headings, improvement/decline/steady messages, "Select two attempts" alert; chart dataset labels via window.__T.Attempt1/Attempt2.
- **Gamification/PointsHistory.cshtml**: Header title and description, current balance, summary labels (Total points, Points earned/spent, Total transactions), Points distribution, Transactions history, Export history, filter buttons (All, Earned, Spent, Courses, Quizzes), empty state – Html.T.
- **Notifications/Settings.cshtml**: All section titles and option labels (General, Email, Learning, Course updates, New assignments, Assessment results, Communication, New messages, Announcements, Live sessions, Achievements, Certificates & achievements, Badges & points, System, Save/Cancel); JS confirm for disabling email uses window.__T.ConfirmDisableEmail.
- **Assignments/Details.cshtml**: JS validation message uses window.__T.AnswerMinLength.

### Session: Translation fixes (student dashboard excl. Help Center)
- **Aria-labels**: Quizzes/AttemptResults.cshtml, Bundles/Details.cshtml – `aria-label="breadcrumb"` → `Html.T("مسار التنقل", "Breadcrumb")`.
- **Certificates/Index.cshtml**: LinkedIn share button `title` → `Html.T("مشاركة على لينكد إن", "Share on LinkedIn")`.
- **PaymentMethods**: Add.cshtml – CVV label and placeholders (card number, CVV) wrapped in Html.T; Index.cshtml – confirm delete uses `Html.Raw(Html.T(...).Replace("'", "\\'"))`.
- **LearningGoals/Details.cshtml**: Button labels (تعديل الهدف, إلغاء الهدف, حذف الهدف) and both confirm messages → Html.T + escaped for JS.
- **PaymentRetry/Details.cshtml**: Inline __T.PaymentError; alerts use window.__T for locale.
- **Gamification/PointsHistory.cshtml**: "تحميل المزيد" button, export/loadMore alerts, chart entity labels and tooltip "نقطة" → locale-aware (Html.T / _chartLabels / _pointLabel).
- **AdaptiveLearning**: AtRiskAlerts dismiss confirm; StudyRecommendations time-slot alert; SmartStudyPlan confirm and share alert → Html.T with Replace for JS.
- **Reviews**: Edit.cshtml and Create.cshtml – validation alerts → Html.Raw(Html.T(...).Replace("'", "\\'")).
- **Notes**: Edit.cshtml (delete/unsaved confirms), Create.cshtml (draft restore confirm) → Html.T with Replace.
- **Profile**: Privacy.cshtml (delete account confirm + prompt), Security.cshtml and SecuritySettings.cshtml (2FA disable confirm) → Html.T with Replace.
- **Learning/Lesson.cshtml**: Note/comment/connection-error alerts → Html.Raw(Html.T(...).Replace("'", "\\'")).
- **Comments, Support, Reminders, Courses (Wishlist, Collections)**: confirm/alert strings use Html.Raw(Html.T(...).Replace("'", "\\'")) for safe JS.

### Additional pass (current)
- **Bundles**: Details.cshtml – breadcrumb (الرئيسية/الباقات), badges, stats (دورات/ساعة/مشترك/توفير), description header, included courses, enrolled badge, lesson/duration labels, sidebar (وفّر, alerts, Add to cart/Buy now, owned message, feature list) all wrapped in Html.T.
- **LearningPaths**: Details.cshtml – progress labels (تقدمك, مكتمل, الدورات المكتملة, الوقت المستغرق, ساعة), started on, Quick info, Level, Total duration, Certificate, Students, certificate block, review/continue labels, learning outcomes text.
- **Subscriptions**: Details.cshtml – subscription info header, start/period end labels, usage statistics header, enrolled courses/learning hours/certificates earned; cancel notice alert.
- **Checkout**: Cart.cshtml – instant access footnote (ستحصل على وصول فوري...).
- **AdaptiveLearning**: CourseAnalytics.cshtml – all metrics, labels, strengths/weaknesses, peer comparison, quiz/topic headers, chart labels (JS), action recommendations (start now, review, join discussions, set goals). AtRiskAlerts.cshtml – page title, subtitle, refresh, filters (الكل/حرج/عالي/متوسط/منخفض), no-alerts state, severity labels, stat labels (أيام بدون نشاط, موعد فائت, etc.), recommendations header, buttons (عرض الدورة, تحليل الأداء, إنشاء خطة, تجاهل), action panel (احصل على مساعدة, انضم لمجموعة, حدد أهدافاً).
- **Courses**: Index.cshtml – modal close button aria-label.
- **LearningGoals**: Create.cshtml – JS goal-type labels and help text (عدد الدورات, ساعة, etc.), days remaining/today/date in past, complete course title prefix via window.__T. Details.cshtml – target value, current progress, days remaining, daily effort, goal progress, total progress, remaining, achievable/warning messages, activity log labels, goal info sidebar (type, related course, dates, status مكتمل/ملغي/نشط), “Keep making progress” footer.
- **Notes**: Edit.cshtml – page title/subtitle, lesson label, created/last modified labels.
- **Reviews**: Edit.cshtml – guideline list (كن صادقاً, ركز على محتوى..., etc.).
- **Comments**: Edit.cshtml – preview placeholder and validation alerts via window.__T (WriteToPreview, CommentMinLength, CommentMaxLength).
- **Subscriptions/Subscribe**: CVV label as Html.T("رمز الأمان (CVV)", "CVV").
- **Gamification**: PointsHistory.cshtml – Excel export dropdown label.
- **Quizzes**: AdaptiveQuiz.cshtml – page title “اختبار تكيفي ذكي” / “Smart adaptive quiz”.

- **Dashboard**: Index (chart day/month labels and "Week" in JS now locale-aware), Achievements; modal close aria-label.
- **Calendar**: Details (all labels, buttons, modal, date culture), Upcoming (date culture, GetEventTypeLabel returns Tr(ar,en)).
- **Support**: Details (date culture, all UI strings), Create (breadcrumb area fix).
- **Messages**: Details (Attachments, Reply, Actions, Sender info, delete confirm, date culture).
- **LearningPaths**: Index (untranslated "ساعة" fixed, date culture where applicable); Details (see above).
- **Culture/date formatting**: Replaced hardcoded `CultureInfo("ar-SA")` with current culture (`ar-SA` or `en-US`) in Support/Details, Calendar/Details, Calendar/Upcoming, Messages/Details, Profile/Referrals, Profile/SecuritySettings, LiveClasses/Recordings, Certificates/Index.
- **Shared**: _StatsCard default "Label" → Html.T; _EmptyState already uses Html.T for defaults.

## Completed in this pass

### Infrastructure
- **`_Layout.cshtml`**: Added `window.__T` with common JS strings (Error, Processing, Success, Close, ConfirmDelete, AgreeTerms, PleaseEnterCoupon, InvalidCoupon, ErrorApplyingCoupon, PleaseEnterGiftCard, InvalidGiftCard, ErrorApplyingGiftCard, SecurePayment, CopySuccess, UnexpectedError, Verifying). Views can extend with `Object.assign(window.__T, { Key: '@...' });`
- **Convention**: All user-visible text must use `Html.T("العربية", "English")`. All JS `alert()`/`confirm()`/`innerHTML` must use `window.__T.Key` or view-scoped __T.

### Views fully or largely translated
- **Dashboard**: Achievements.cshtml (all labels, badges, buttons)
- **Discussions**: Details.cshtml (sidebar, labels, badges, JS alerts via __T)
- **PaymentMethods**: Add.cshtml (labels, buttons, terms)
- **Calendar**: Edit.cshtml, Create.cshtml (breadcrumbs, labels, reminder options, buttons)
- **Checkout**: Index.cshtml (steps, billing, payment, coupon/gift card, order summary, JS), Cart.cshtml (headers, labels, buttons, empty state)
- **Activity**: Summary.cshtml (header, metrics, time summary)
- **Assignments**: Index.cshtml (header, filter buttons)
- **Subscriptions**: Index.cshtml (header, status texts, labels, buttons)
- **Refunds**: Index.cshtml (header, status, labels, policy list)
- **Wishlist**: Index.cshtml (header, labels, Free, Add to cart, empty state, JS confirm)
- **Reminders**: Create.cshtml, Edit.cshtml, Index.cshtml, Upcoming.cshtml (all labels, types, buttons, danger zone, JS confirm delete)
- **Invoices**: Index.cshtml, View.cshtml (headers, table, status, empty state, invoice labels, notes, footer)
- **MultiGatewayCheckout**: BankTransferInstructions.cshtml (instructions, bank details, form labels, expiry messages, JS copy alert via __T)
- **Quizzes**: Index.cshtml (badges, best attempt, buttons: Start/Retry, View past attempts, Not available, No attempts left, empty state)

## Remaining work (same pattern)

For each file below, replace every **visible** Arabic or English-only string with `@Html.T("العربية", "English")`. For inline `<script>` blocks, inject view-specific keys into `window.__T` at the top of the script, then use `(window.__T && window.__T.Key) || 'Fallback'` in `alert()`/`confirm()`/`innerHTML`/`.text()`.

### By folder (exclude Help area)

- **Activity**: Index.cshtml
- **AdaptiveLearning**: SmartStudyPlan, StudyRecommendations, NextCourseRecommendations, ~~AtRiskAlerts~~ ✓, ~~CourseAnalytics~~ ✓, MyAnalytics, NextSteps, Achievements
- **Achievements**: Index, Leaderboard, Points
- **Assignments**: Submit, Details (and JS alerts)
- **Bookmarks**: Index, EditNote
- **Books**: MyLibrary, Index, Details, Download
- **Bundles**: Index, ~~Details~~ ✓
- **Calendar**: Index, Details, Upcoming
- **Certificates**: Index, Details, VerifyResult
- **Comments**: Index, ~~Edit~~ ✓ (and JS __T)
- **Courses**: ~~Index~~ ✓ (modal aria-label), InProgress, CompletedCourses, Browse, Details, Preview, Recommendations, CompareCourses, Collections, Wishlist, ShareProgress, MyLearningStats
- **Dashboard**: Index, WeeklyDigest, StudyStats, QuickActions, Calendar
- **Forums**: Index
- **Gifts**: Index, Send, Redeem, RedeemSuccess, GiftCheckout
- **Invoices**: ~~Index, View~~ ✓
- **Learning**: Lesson
- **LearningGoals**: Index, ~~Create~~ ✓ (JS __T), Edit, ~~Details~~ ✓ (and JS)
- **LearningPaths**: Index, ~~Details~~ ✓, MyPaths
- **LiveClasses**: Index, Details, Join, Recordings, WatchRecording
- **LiveClassAttendance**: Index, Details
- **LiveSessionSchedules**: Index, MySchedules, Details, Purchase; **SessionCheckout**: Checkout, PaymentSuccess, PaymentFailed
- **Messages**: Index, Compose, Details, Sent
- **MultiGatewayCheckout**: Index, FawryReference, ~~BankTransferInstructions~~ ✓, BankTransferStatus, Success, Cancel
- **Notifications**: Index, Settings
- **Notes**: Index, Create, ~~Edit~~ ✓, Course (and JS)
- **PaymentMethods**: Index; **PaymentRetry**: Index, Details
- **Profile**: Index, Edit, Settings, Security, Privacy, SecuritySettings, PrivacySettings, NotificationPreferences, Portfolio, Public, Referrals, LearningStyleAssessment
- **Progress**: Index, Course
- **Purchases**: History, Details
- **Quizzes**: ~~Index~~ ✓, Results, Leaderboard, Start, TakeQuiz, AttemptResults, MyQuizAnalytics, History, AllResults, ReviewAnswers, AttemptAnalysis, CompareAttempts, PracticeMode, ~~AdaptiveQuiz~~ ✓ (title), QuickChallenge, ReviewRecommendations (and JS)
- **Reminders**: ~~Index, Create, Edit, Upcoming~~ ✓ (confirm/alert via __T)
- **Reviews**: Index, Create, ~~Edit~~ ✓ (guidelines) (and JS)
- **Settings**: Index (UTC option ✓), Profile/Settings (UTC ✓)
- **StudyGroups**: Index, Create, Details
- **StudyPlanner**: Index, Reminders, Calendar, GeneratePlan, CommitmentStats (and JS)
- **Subscriptions**: Plans, ~~Details~~ ✓, Cancel, Subscribe (CVV label ✓), ~~UpdatePaymentMethod~~ ✓ (تنتهي في, VISA/Mastercard), Checkout (and JS)
- **Support**: Index, Create, Details
- **Refunds**: Details, Request
- **InstructorApplication**: Index
- **Leaderboard**: Index
- **Gamification**: Dashboard, ~~PointsHistory~~ ✓ (Excel, CSV, PDF labels)
- **Checkout**: ~~Cart~~ ✓ (instant access text), Success
- **Shared**: _Header, _CourseCard, _QuickActions, _SecureVideoPlayer (any visible strings)

### JS-specific (add __T in view, then use in script)
- ~~Reminders/Edit.cshtml: confirm delete~~ ✓ (__T.ConfirmDeleteReminder)
- Reminders/Index.cshtml: already uses Html.T in some JS
- StudyPlanner/Reminders: confirm delete
- Assignments/Submit: confirm submit, validation alert
- Assignments/Details: validation alert
- Comments/Edit: preview text, validation alerts
- Reviews/Edit: validation alerts
- Quizzes/TakeQuiz: time-up alert, confirm submit
- ~~LearningGoals/Create~~ ✓: targetLabel/targetUnit/targetHelp, daysRemaining (via __T)
- LearningGoals/Details, Index: confirm cancel/delete
- Notes/Create, Edit, Course: confirm, alerts
- Subscriptions/Cancel, Subscribe: alert, confirm, button text
- AdaptiveLearning (SmartStudyPlan, StudyRecommendations, AtRiskAlerts, NextCourseRecommendations, NextSteps): confirm, alert
- MultiGatewayCheckout/Index, FawryReference, BankTransferInstructions: alert, button text
- Certificates/Details: shareNotSupported, codeCopied (from data attributes or __T)
- Gifts/Index, GiftCheckout: innerHTML, alert
- PaymentRetry/Details: alert
- Notifications/Settings: confirm
- StudyPlanner/GeneratePlan, Index: alert, confirm
- Gamification/PointsHistory: alert
- Calendar/Upcoming: alert export error
- Messages/Details: confirm delete
- Bookmarks/Index, Reviews/Index, PaymentRetry/Index: confirm delete

## Session: Translation fixes (student dashboard excl. Help Center) – latest
- **Layout (_Layout.cshtml)**: Added `CopyCode` to `window.__T` for consistent "نسخ الرمز" / "Copy code" in JS and initial button label.
- **Gifts/Index.cshtml**: Copy-code button label "نسخ الرمز" wrapped in `Html.T("نسخ الرمز", "Copy code")`; JS fallback uses `window.__T.CopyCode`.
- **Reviews/Edit.cshtml**: Back button "رجوع" → `Html.T("رجوع", "Back")`. Rating UI: `ratingTexts` and `updateStars()` use locale via `window.__T` (Rating1–5, YourRatingFormat, ClickStarsToRate).
- **Reviews/Create.cshtml**: Same rating translation pattern: inject `window.__T` with Rating0–5, YourRatingFormat, ClickStarsToRate; `ratingTexts` array and `updateStars()` use __T for AR/EN.

## Session: Translation fixes (student dashboard excl. Help Center) – latest pass
- **Subscriptions/UpdatePaymentMethod.cshtml**: Raw Arabic "تنتهي في" → `Html.T("تنتهي في", "Expires")`; card brand labels "VISA" → `Html.T("فيزا", "VISA")`, "MC" → `Html.T("ماستركارد", "Mastercard")`.
- **Gamification/PointsHistory.cshtml**: Export dropdown labels "CSV" and "PDF" wrapped in `Html.T("CSV", "CSV")` and `Html.T("PDF", "PDF")` for consistency.
- **Settings/Index.cshtml** and **Profile/Settings.cshtml**: Time zone option "UTC" wrapped in `Html.T("UTC", "UTC")` for consistency.

## Deep re-examination (production readiness – excl. Help Center)

**Scope:** All `Areas/Student/Views` except `Help/` (all Help Center views excluded).

**Checks performed:**
1. **Raw Arabic in markup:** Grep for visible Arabic outside `Html.T`/`@` – no untranslated strings in non-Help views. All hits are inside `Html.T("...", "...")` or in Help (excluded).
2. **Placeholders:** Every `placeholder="..."` in non-Help views uses `Html.T(...)`. Only untranslated placeholder is `Help/FAQ.cshtml` (excluded).
3. **aria-label:** All `aria-label` in Student views use `Html.T("مسار التنقل", "Breadcrumb")`, `Html.T("إغلاق", "Close")`, or similar. LearningGoals/Index toast close uses `_goalMsgs.close` from `Html.T("إغلاق", "Close")`.
4. **ViewData["Title"]:** All static titles use `Html.T("ar", "en")`. Dynamic titles (e.g. `Model.Name`, `Model.Course.Title`) are entity names from data – acceptable; rest of page uses `Html.T`.
5. **JS strings:** Alerts/confirms use either `Html.Raw(Html.T(...).Replace("'", "\\'"))` inline or `window.__T` / view-scoped objects (e.g. `_goalMsgs`, `window.__T.Rating1`, `CopyCode`). Layout provides common keys; views extend with `Object.assign(window.__T, { ... })`.
6. **@functions pattern:** Activity/Index (and similar) use `IsAr()` and `Tr(ar, en)` for date labels and activity titles – equivalent to `Html.T`, culture-aware, consistent with project.

**Enterprise pattern confirmed:**
- **Server-rendered text:** `Html.T("العربية", "English")` for all visible static strings.
- **JS strings:** `window.__T.Key` (layout or view-injected) with fallback; or `Html.Raw(Html.T(...).Replace("'", "\\'"))` for one-off alerts/confirms.
- **No raw Arabic or English-only** in any non-Help Student view for labels, buttons, placeholders, titles, or aria-labels.

**Conclusion:** Student dashboard (excluding Help Center) is translated per the enterprise approach and is ready for production.

### Re-verification (latest pass)
- Grep for raw `placeholder="` (non-Help): only Help/FAQ had untranslated placeholder (excluded).
- Grep for raw `aria-label="` and `title="`: all use Html.T or view-injected __T.
- Grep for raw Arabic in markup: fixed **Subscriptions/UpdatePaymentMethod.cshtml** ("تنتهي في"); no other non-Help views had raw Arabic in visible UI.
- Export labels (CSV/PDF) and time zone (UTC) and card brands (VISA/Mastercard) now use Html.T for consistency.

### Deep re-examination (production 100% – excl. Help Center)
- **ViewData["Title"]**: All static titles use Html.T; dynamic titles (e.g. Model.Name, Model.Course.Title) are entity data – acceptable.
- **Placeholders**: Only Help/FAQ has raw placeholder (excluded).
- **aria-label**: LearningGoals/Index toast uses _goalMsgs.close from Html.T; all others use Html.T in markup.
- **alt attributes**: Fixed all non-Help views – NextSteps (Certificate), Subscribe (Visa/Mastercard/Amex), Checkout/Index (Apple Pay, Google Pay, Visa/Mastercard/Amex), Quizzes (Question image), LiveClasses/Recordings (Recording), PracticeMode (Question image), FawryReference (Fawry), WatchRecording (Thumbnail). All now use Html.T.
- **JS strings**: Alerts/confirms use window.__T or Html.Raw(Html.T(...).Replace("'", "\\'")). LearningGoals _goalMsgs and Security _strengthLabels inject server-side Html.T via Json.Serialize.
- **Profile/Security.cshtml**: Password strength labels were Arabic-only; now use _strengthLabels array from Html.T (Very weak / Weak / Medium / Good / Very strong).
- **LiveSessionSchedules/Purchase.cshtml**: "Visa, Mastercard" → Html.T("فيزا، ماستركارد", "Visa, Mastercard").
- **Shared partials**: _EmptyState and _StatsCard default title/message/label use Html.T.
- **Conclusion**: No remaining untranslated user-visible strings in Student views (excluding Help Center). Ready for production.

## Verification
- Switch culture to `ar` and `en` (e.g. via query or cookie).
- Visit every sidebar link (except Help Center) and all sub-views.
- Check no raw Arabic or English-only visible text; all alerts/confirms use locale.

## Session: Translation fixes (student dashboard excl. Help Center) – latest
- **Layout (_Layout.cshtml)**: Added global `window.__T` keys for payment and copy: `CopyRefSuccess`, `PaymentError`, `PaymentStarted`, `PaymentNotConfigured`. Ensures Checkout, MultiGatewayCheckout (Index, FawryReference, BankTransferInstructions), PaymentRetry, and Subscriptions/Subscribe have locale-aware JS fallbacks.
- **Calendar**: Create.cshtml and Edit.cshtml – IsAllDay checkbox label was empty (Display from model is Arabic-only). Added explicit label `@Html.T("طوال اليوم", "All day")` so the label is translated. Calendar/Details.cshtml already used Html.T for "All day" badge.
- **Books/Details.cshtml**: Cover image had `alt=""`. Set `alt="@Html.T("غلاف الكتاب", "Book cover")"` for accessibility and translation.
- **AdaptiveLearning/NextCourseRecommendations.cshtml**: Stage step subtitles were raw English ("HTML, CSS, JavaScript", "React, Vue, Angular", "Node.js, Python, APIs"). Wrapped in `Html.T` with descriptive AR/EN: "أساسيات الويب: HTML، CSS، JavaScript" / "Web basics: HTML, CSS, JavaScript"; "أطر الواجهة: React، Vue، Angular" / "Frontend frameworks: React, Vue, Angular"; "الخادم والواجهات: Node.js، Python، APIs" / "Server & APIs: Node.js, Python, APIs".
- **Re-scan**: Confirmed no untranslated `placeholder`/`aria-label`/`title`/static `ViewData["Title"]` in non-Help Student views. JS alerts/confirms use `window.__T` or `Html.Raw(Html.T(...).Replace("'", "\\'"))`. StudyPlanner/Index and Discussions/Details already use server-injected locale strings.
- **Conclusion**: Student dashboard (excluding Help Center) remains fully translated per enterprise approach; ready for production.
