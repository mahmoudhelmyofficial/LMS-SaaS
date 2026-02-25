# Admin Dashboard Translation Audit (Excluding Help Center)

## Enterprise approach (confirmed)

- **Views:** All user-visible strings use `Html.T("Arabic", "English")` from `LMS.Extensions.CultureExtensions`. Language is determined by `CultureInfo.CurrentUICulture` (ar = Arabic, else = English).
- **Controllers:** TempData/Set*Message use either:
  - **Two-argument overload:** `SetSuccessMessage("Arabic", "English")` etc. on `AdminBaseController` (calls `CultureExtensions.T` internally), or
  - **Pre-translated string:** `SetSuccessMessage(CultureExtensions.T("Arabic", "English"))` or `string.Format(CultureExtensions.T("...{0}...", "...{0}..."), value)` for interpolated messages.
- **Exclusion:** All views under `Areas/Admin/Views/Help/` are excluded from translation fixes.

---

## What is fully translated and production-ready

### 1. Infrastructure
- `Extensions/CultureExtensions.cs`: Static `T(arabic, english)` + `Html.T(arabic, english)`.
- `Areas/Admin/Controllers/AdminBaseController.cs`: Overloads `SetSuccessMessage(ar, en)`, `SetErrorMessage(ar, en)`, `SetWarningMessage(ar, en)`, `SetInfoMessage(ar, en)` for culture-aware messages.

### 2. Controllers (all TempData/Set*Message translated)
- DisputesController, CurrenciesController, WithdrawalMethodsController, PaymentsController  
- LearningPathsController, ContentDripController, DashboardController, CertificatesController  
- PushNotificationSettingsController, SubscriptionsController, CommissionSettingsController  
- CouponsController, ReviewsController, LiveClassesController, BooksController  
- QuestionBankController, BundlesController, EmailTemplatesController (literal messages; interpolated test-email messages may still need translation)  
- RefundsController, ManualPaymentsController, CategoriesController  

### 3. Views (all user-visible strings use Html.T)
- **PaymentSettings/Index.cshtml** – Labels, placeholders, buttons, gateways, currencies.  
- **VideoSettings/Index.cshtml** – Playback, security, API, storage, formats, actions.  
- **StorageSettings/Index.cshtml** – Provider, path, CDN, quota, alerts.  
- **MediaSettings/Index.cshtml** – File types, image processing, thumbnails, watermark, buttons.  
- **ManualPayments/Details.cshtml** – IBAN label.  
- **Shared:** `_Sidebar.cshtml`, `_Messages.cshtml` already use `Html.T`; Help area excluded.

---

## What remains (to reach 100% production-ready)

### Controllers still with one-arg literal or interpolated messages
Replace every remaining call with the two-arg form or with `string.Format(CultureExtensions.T("...{0}...", "...{0}..."), value)` as appropriate. Add `using LMS.Extensions;` where CultureExtensions is used.

| Controller | Pattern |
|------------|--------|
| InstructorsController | Many Set*Message("Arabic only") and interpolated; use Set*Message("ar", "en") or string.Format(CultureExtensions.T(...), ...). |
| AnalyticsController | SetErrorMessage / SetSuccessMessage literals. |
| AdvancedReportsController | Literals + interpolated (e.g. duplicate template, report scheduled). |
| ScheduledReportsController | Literals. |
| CoursesController | Many literals + interpolated (status, suspend, copy, etc.). |
| SettingsController | Literals (general, language, appearance, integrations, SMS, video). |
| SupportController | Literals. |
| And others | See grep: `Set(Success|Error|Warning|Info)Message\(\"[^\"]+\"\);` and `Set*Message(\$"` in Areas/Admin/Controllers (excluding Help). |

### Views (excluding Help)
- Any Admin view not listed in “What is fully translated” may still contain raw Arabic/English (labels, placeholders, buttons, options).  
- **Check:** Search for `>Arabic text<` or `"English only"` or `placeholder="..."` without `@Html.T` in `Areas/Admin/Views/**/*.cshtml` (excluding `**/Help/**`).  
- **Fix:** Wrap each user-visible string in `@Html.T("Arabic", "English")`.

---

## How to complete the rest (same enterprise approach)

1. **Literal controller messages**  
   Replace:  
   `SetSuccessMessage("تم ...");`  
   with:  
   `SetSuccessMessage("تم ...", "English message.");`  
   (Same for SetErrorMessage, SetWarningMessage, SetInfoMessage.)

2. **Interpolated controller messages**  
   Replace:  
   `SetSuccessMessage($"تم ... {value} ...");`  
   with:  
   `SetSuccessMessage(string.Format(CultureExtensions.T("تم ... {0} ...", "English ... {0} ..."), value));`  
   Ensure the controller has `using LMS.Extensions;`.

3. **Views**  
   Ensure every user-visible string (labels, placeholders, buttons, titles, options) uses `@Html.T("Arabic", "English")`. No raw Arabic or English only.

4. **Build**  
   Run `dotnet build` in the solution root and fix any compile errors.

---

## Assurance summary

- **Approach:** Aligned with the existing enterprise pattern (culture from `CurrentUICulture`, single `T`/`Html.T` contract, Admin base controller overloads).  
- **Implemented:** All infrastructure, listed controllers, and listed views are translated and consistent; no Help Center changes.  
- **Remaining:** Other Admin controllers and any remaining Admin views (excluding Help) need the same literal/interpolated and view patterns applied as above.  
- **Conflicts:** No intentional changes to Help area, layout, or culture logic; TempData display in `_Messages.cshtml` unchanged.  

After applying the “How to complete” steps to the remaining controllers and views, the Admin dashboard (excluding Help Center) will be **100% translated and ready for production** with the same enterprise approach.
