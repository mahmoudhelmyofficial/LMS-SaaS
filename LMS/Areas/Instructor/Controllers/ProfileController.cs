using LMS.Areas.Instructor.ViewModels;
using LMS.Data;
using LMS.Helpers;
using LMS.Services.Interfaces;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace LMS.Areas.Instructor.Controllers;

/// <summary>
/// الملف الشخصي للمدرس - Instructor Profile Controller
/// Enterprise-level implementation using service layer pattern
/// </summary>
public class ProfileController : InstructorBaseController
{
    private readonly IInstructorProfileService _profileService;
    private readonly IDropdownConfigService _dropdownService;
    private readonly ICurrentUserService _currentUserService;
    private readonly ICurrencyService _currencyService;
    private readonly ApplicationDbContext _context;
    private readonly ILogger<ProfileController> _logger;

    public ProfileController(
        IInstructorProfileService profileService,
        IDropdownConfigService dropdownService,
        ICurrentUserService currentUserService,
        ICurrencyService currencyService,
        ApplicationDbContext context,
        ILogger<ProfileController> logger)
    {
        _profileService = profileService;
        _dropdownService = dropdownService;
        _currentUserService = currentUserService;
        _currencyService = currencyService;
        _context = context;
        _logger = logger;
    }

    #region Profile

    /// <summary>
    /// Set default currency for view
    /// </summary>
    private async Task SetDefaultCurrencyAsync()
    {
        await base.SetDefaultCurrencyAsync(_context, _currencyService, null, _logger);
    }

    /// <summary>
    /// عرض الملف الشخصي - View Profile
    /// </summary>
    public async Task<IActionResult> Index()
    {
        try
        {
            await SetDefaultCurrencyAsync();
            
            var userId = _currentUserService.UserId;
            if (string.IsNullOrEmpty(userId))
            {
                _logger.LogWarning("Index: UserId is null or empty");
                return RedirectToAction("Login", "Account", new { area = "" });
            }

            var profile = await _profileService.GetProfileAsync(userId);
            if (profile == null)
            {
                _logger.LogWarning("Index: Profile not found for userId {UserId}", userId);
                SetWarningMessage("تعذر تحميل الملف الشخصي. يرجى المحاولة مرة أخرى.");
                return View(null);
            }

            // Get qualifications for display
            try
            {
                var qualifications = await _profileService.GetQualificationsAsync(userId);
                ViewBag.Qualifications = qualifications ?? new List<QualificationDto>();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading qualifications for userId {UserId}", userId);
                ViewBag.Qualifications = new List<QualificationDto>();
            }

            // Get profile completeness
            try
            {
                var completeness = await _profileService.GetProfileCompletenessAsync(userId);
                ViewBag.ProfileCompleteness = completeness ?? new ProfileCompletenessDto();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading profile completeness for userId {UserId}", userId);
                ViewBag.ProfileCompleteness = new ProfileCompletenessDto();
            }

            return View(profile);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in ProfileController.Index for userId {UserId}", _currentUserService.UserId);
            SetErrorMessage("حدث خطأ غير متوقع أثناء تحميل الملف الشخصي. يرجى المحاولة مرة أخرى.");
            return View(null);
        }
    }

    /// <summary>
    /// تعديل الملف الشخصي - Edit Profile (GET)
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> Edit()
    {
        try
        {
            var userId = _currentUserService.UserId;
            if (string.IsNullOrEmpty(userId))
            {
                _logger.LogWarning("Edit GET: UserId is null or empty");
                return RedirectToAction("Login", "Account", new { area = "" });
            }

            var profile = await _profileService.GetProfileForEditAsync(userId);
            if (profile == null)
            {
                _logger.LogWarning("Edit GET: Profile not found for userId {UserId}", userId);
                SetErrorMessage("ملف المدرس غير موجود");
                return RedirectToAction(nameof(Index));
            }

            try
            {
                await PopulateEditDropdowns();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error populating dropdowns in Edit GET for userId {UserId}", userId);
                // Continue with default values
            }

            var viewModel = new InstructorProfileEditViewModel
            {
                FirstName = profile.FirstName ?? string.Empty,
                LastName = profile.LastName ?? string.Empty,
                Bio = profile.Bio,
                InstructorBio = profile.InstructorBio,
                Headline = profile.Headline,
                Website = profile.Website,
                Country = profile.Country,
                City = profile.City,
                TimeZone = profile.TimeZone ?? "Africa/Cairo",
                Language = profile.Language ?? "ar"
            };

            return View(viewModel);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in ProfileController.Edit GET for userId {UserId}", _currentUserService.UserId);
            SetErrorMessage("حدث خطأ أثناء تحميل صفحة التعديل");
            return RedirectToAction(nameof(Index));
        }
    }

    /// <summary>
    /// حفظ الملف الشخصي - Save Profile (POST)
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(InstructorProfileEditViewModel model)
    {
        try
        {
            var userId = _currentUserService.UserId;
            if (string.IsNullOrEmpty(userId))
            {
                _logger.LogWarning("Edit POST: UserId is null or empty");
                return RedirectToAction("Login", "Account", new { area = "" });
            }

            // Additional validation
            if (!string.IsNullOrWhiteSpace(model.InstructorBio) && model.InstructorBio.Length < 50)
            {
                ModelState.AddModelError(nameof(model.InstructorBio), "السيرة الذاتية يجب أن تكون 50 حرفاً على الأقل");
            }

            // Validate website URL if provided
            if (!string.IsNullOrWhiteSpace(model.Website))
            {
                if (!Uri.TryCreate(model.Website, UriKind.Absolute, out var uriResult) ||
                    (uriResult.Scheme != Uri.UriSchemeHttp && uriResult.Scheme != Uri.UriSchemeHttps))
                {
                    ModelState.AddModelError(nameof(model.Website), "رابط الموقع غير صحيح. يجب أن يبدأ بـ http:// أو https://");
                }
            }

            if (!ModelState.IsValid)
            {
                try
                {
                    await PopulateEditDropdowns();
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error populating dropdowns in Edit POST");
                }
                return View(model);
            }

            var dto = new UpdateInstructorProfileDto
            {
                FirstName = model.FirstName?.Trim(),
                LastName = model.LastName?.Trim(),
                Bio = model.Bio?.Trim(),
                InstructorBio = model.InstructorBio?.Trim(),
                Headline = model.Headline?.Trim(),
                Website = model.Website?.Trim(),
                Country = model.Country?.Trim(),
                City = model.City?.Trim(),
                TimeZone = model.TimeZone?.Trim(),
                Language = model.Language?.Trim()
            };

            var result = await _profileService.UpdateProfileAsync(userId, dto);
            
            if (result.IsSuccess)
            {
                _logger.LogInformation("Profile updated successfully for userId {UserId}", userId);
                SetSuccessMessage("تم تحديث الملف الشخصي بنجاح");
                return RedirectToAction(nameof(Index));
            }

            _logger.LogWarning("Profile update failed for userId {UserId}: {Error}", userId, result.Error);
            SetErrorMessage(result.Error ?? "حدث خطأ أثناء تحديث الملف الشخصي");
            
            try
            {
                await PopulateEditDropdowns();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error populating dropdowns after failed update");
            }
            
            return View(model);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in ProfileController.Edit POST for userId {UserId}. Message: {Message}, Inner: {Inner}",
                _currentUserService.UserId, ex.Message, ex.InnerException?.Message);
            SetErrorMessage("حدث خطأ غير متوقع أثناء تحديث الملف الشخصي");
            
            try
            {
                await PopulateEditDropdowns();
            }
            catch
            {
                // Ignore dropdown errors
            }
            
            return View(model);
        }
    }

    #endregion

    #region Settings

    /// <summary>
    /// الإعدادات العامة - General Settings (GET)
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> Settings()
    {
        try
        {
            await SetDefaultCurrencyAsync();
            var userId = _currentUserService.UserId;
            if (string.IsNullOrEmpty(userId))
            {
                _logger.LogWarning("Settings GET: UserId is null or empty");
                return RedirectToAction("Login", "Account", new { area = "" });
            }

            var settings = await _profileService.GetSettingsAsync(userId);
            if (settings == null)
            {
                _logger.LogWarning("Settings GET: Settings not found for userId {UserId}", userId);
                SetErrorMessage("تعذر تحميل الإعدادات");
                return RedirectToAction(nameof(Index));
            }

            try
            {
                await PopulateSettingsDropdowns();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error populating settings dropdowns");
            }

            var viewModel = new InstructorSettingsViewModel
            {
                FirstName = settings.FirstName ?? string.Empty,
                LastName = settings.LastName ?? string.Empty,
                Email = settings.Email ?? string.Empty,
                Bio = settings.Bio,
                InstructorBio = settings.InstructorBio,
                Headline = settings.Headline,
                Website = settings.Website,
                Country = settings.Country,
                City = settings.City,
                TimeZone = settings.TimeZone ?? "Africa/Cairo",
                Language = settings.Language ?? "ar",
                PayoutMethod = settings.PayoutMethod,
                PayPalEmail = settings.PayPalEmail,
                BankName = settings.BankName,
                BankAccountName = settings.BankAccountName,
                BankAccountNumber = settings.BankAccountNumber,
                IBAN = settings.IBAN,
                SwiftCode = settings.SwiftCode,
                TotalEarnings = settings.TotalEarnings,
                AvailableBalance = settings.AvailableBalance,
                CommissionRate = settings.CommissionRate,
                IsApproved = settings.IsApproved,
                AverageRating = settings.AverageRating,
                TotalStudents = settings.TotalStudents
            };

            // Get notification preferences
            try
            {
                var notificationPrefs = await _profileService.GetNotificationPreferencesAsync(userId);
                viewModel.EmailNotifications = notificationPrefs?.EmailOnNewEnrollment ?? true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading notification preferences");
                viewModel.EmailNotifications = true;
            }

            return View(viewModel);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in Settings GET for userId {UserId}", _currentUserService.UserId);
            SetErrorMessage("حدث خطأ أثناء تحميل الإعدادات");
            return RedirectToAction(nameof(Index));
        }
    }

    /// <summary>
    /// حفظ الإعدادات العامة - Save General Settings (POST)
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Settings(InstructorSettingsViewModel model)
    {
        try
        {
            var userId = _currentUserService.UserId;
            if (string.IsNullOrEmpty(userId))
                return RedirectToAction("Login", "Account", new { area = "" });

            // Validate website URL if provided (same as Edit)
            if (!string.IsNullOrWhiteSpace(model.Website))
            {
                if (!Uri.TryCreate(model.Website, UriKind.Absolute, out var uriResult) ||
                    (uriResult.Scheme != Uri.UriSchemeHttp && uriResult.Scheme != Uri.UriSchemeHttps))
                {
                    ModelState.AddModelError(nameof(model.Website), "رابط الموقع غير صحيح. يجب أن يبدأ بـ http:// أو https://");
                }
            }

            if (!ModelState.IsValid)
            {
                try { await PopulateSettingsDropdowns(); } catch (Exception ex) { _logger.LogError(ex, "Error populating settings dropdowns on validation failure"); }
                await SetDefaultCurrencyAsync();
                return View(model);
            }

            var dto = new UpdateInstructorSettingsDto
            {
                FirstName = model.FirstName?.Trim(),
                LastName = model.LastName?.Trim(),
                Bio = model.Bio?.Trim(),
                InstructorBio = model.InstructorBio?.Trim(),
                Headline = model.Headline?.Trim(),
                Website = model.Website?.Trim(),
                Country = model.Country?.Trim(),
                City = model.City?.Trim(),
                TimeZone = string.IsNullOrWhiteSpace(model.TimeZone) ? "Africa/Cairo" : model.TimeZone.Trim(),
                Language = string.IsNullOrWhiteSpace(model.Language) ? "ar" : model.Language.Trim(),
                PayoutMethod = model.PayoutMethod?.Trim(),
                PayPalEmail = model.PayPalEmail?.Trim(),
                BankName = model.BankName?.Trim(),
                BankAccountName = model.BankAccountName?.Trim(),
                BankAccountNumber = model.BankAccountNumber?.Trim(),
                IBAN = model.IBAN?.Trim(),
                SwiftCode = model.SwiftCode?.Trim()
            };

            var result = await _profileService.UpdateSettingsAsync(userId, dto);

            if (result.IsSuccess)
            {
                // Persist notification preference from Settings page (EmailNotifications = EmailOnNewEnrollment)
                try
                {
                    var prefs = await _profileService.GetNotificationPreferencesAsync(userId);
                    var notificationDto = new UpdateNotificationPreferencesDto
                    {
                        EmailOnNewEnrollment = model.EmailNotifications,
                        EmailOnNewReview = prefs?.EmailOnNewReview ?? true,
                        EmailOnNewQuestion = prefs?.EmailOnNewQuestion ?? true,
                        EmailOnNewMessage = prefs?.EmailOnNewMessage ?? true,
                        EmailOnPaymentReceived = prefs?.EmailOnPaymentReceived ?? true,
                        EmailOnWithdrawalProcessed = prefs?.EmailOnWithdrawalProcessed ?? true,
                        EmailWeeklyDigest = prefs?.EmailWeeklyDigest ?? true,
                        EmailMonthlyReport = prefs?.EmailMonthlyReport ?? true,
                        EmailMarketingUpdates = prefs?.EmailMarketingUpdates ?? false,
                        PushNotifications = prefs?.PushNotifications ?? true,
                        InAppNotifications = prefs?.InAppNotifications ?? true
                    };
                    await _profileService.UpdateNotificationPreferencesAsync(userId, notificationDto);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error saving notification preference from Settings for userId {UserId}", userId);
                    // Settings saved; notification pref is best-effort
                }

                SetSuccessMessage("تم حفظ الإعدادات بنجاح");
                return RedirectToAction(nameof(Settings));
            }

            _logger.LogWarning("Settings update failed for userId {UserId}: {Error}", userId, result.Error);
            SetErrorMessage(result.Error ?? "حدث خطأ أثناء حفظ الإعدادات");
            try { await PopulateSettingsDropdowns(); } catch (Exception ex) { _logger.LogError(ex, "Error populating settings dropdowns after failed update"); }
            await SetDefaultCurrencyAsync();
            return View(model);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in ProfileController.Settings POST for userId {UserId}. Message: {Message}, Inner: {Inner}",
                _currentUserService.UserId, ex.Message, ex.InnerException?.Message);
            SetErrorMessage("حدث خطأ غير متوقع أثناء حفظ الإعدادات");
            try { await PopulateSettingsDropdowns(); } catch { /* ignore */ }
            await SetDefaultCurrencyAsync();
            return View(model);
        }
    }

    #endregion

    #region Payment Settings

    /// <summary>
    /// إعدادات الدفع - Payment Settings (GET)
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> PaymentSettings()
    {
        try
        {
            await SetDefaultCurrencyAsync();
            
            var userId = _currentUserService.UserId;
            if (string.IsNullOrEmpty(userId))
            {
                _logger.LogWarning("PaymentSettings GET: UserId is null or empty");
                return RedirectToAction("Login", "Account", new { area = "" });
            }

            var paymentSettings = await _profileService.GetPaymentSettingsAsync(userId);
            if (paymentSettings == null)
            {
                _logger.LogWarning("PaymentSettings GET: Payment settings not found for userId {UserId}", userId);
                SetErrorMessage("تعذر تحميل إعدادات الدفع");
                return RedirectToAction(nameof(Index));
            }

            var viewModel = new InstructorPaymentSettingsViewModel
            {
                PayoutMethod = paymentSettings.PayoutMethod,
                PayPalEmail = paymentSettings.PayPalEmail,
                BankName = paymentSettings.BankName,
                BankAccountName = paymentSettings.BankAccountName,
                BankAccountNumber = paymentSettings.BankAccountNumber,
                IBAN = paymentSettings.IBAN,
                SwiftCode = paymentSettings.SwiftCode,
                MobileWalletNumber = paymentSettings.MobileWalletNumber,
                MobileWalletProvider = paymentSettings.MobileWalletProvider,
                WiseEmail = paymentSettings.WiseEmail,
                StripeAccountId = paymentSettings.StripeAccountId
            };

            try
            {
                await PopulatePaymentDropdowns();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error populating payment dropdowns");
            }

            // Add extra data to ViewBag
            ViewBag.AvailableBalance = paymentSettings.AvailableBalance;
            ViewBag.PendingBalance = paymentSettings.PendingBalance;
            ViewBag.MinimumWithdrawal = paymentSettings.MinimumWithdrawal;
            ViewBag.IsPaymentMethodVerified = paymentSettings.IsPaymentMethodVerified;
            ViewBag.LastVerifiedAt = paymentSettings.LastVerifiedAt;

            return View(viewModel);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in PaymentSettings GET for userId {UserId}", _currentUserService.UserId);
            SetErrorMessage("حدث خطأ أثناء تحميل إعدادات الدفع");
            return RedirectToAction(nameof(Index));
        }
    }

    /// <summary>
    /// حفظ إعدادات الدفع - Save Payment Settings (POST)
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> PaymentSettings(InstructorPaymentSettingsViewModel model)
    {
        try
        {
            var userId = _currentUserService.UserId;
            if (string.IsNullOrEmpty(userId))
            {
                _logger.LogWarning("PaymentSettings POST: UserId is null or empty");
                return RedirectToAction("Login", "Account", new { area = "" });
            }

            // Additional validation based on payment method
            if (string.IsNullOrWhiteSpace(model.PayoutMethod))
            {
                ModelState.AddModelError(nameof(model.PayoutMethod), "يجب اختيار طريقة الدفع");
            }
            else
            {
                // Validate based on selected payment method
                if (model.PayoutMethod == "PayPal")
                {
                    if (string.IsNullOrWhiteSpace(model.PayPalEmail))
                    {
                        ModelState.AddModelError(nameof(model.PayPalEmail), "بريد PayPal مطلوب");
                    }
                    else if (!System.Text.RegularExpressions.Regex.IsMatch(model.PayPalEmail, @"^[^@\s]+@[^@\s]+\.[^@\s]+$"))
                    {
                        ModelState.AddModelError(nameof(model.PayPalEmail), "بريد إلكتروني غير صحيح");
                    }
                }
                else if (model.PayoutMethod == "BankTransfer")
                {
                    if (string.IsNullOrWhiteSpace(model.BankName))
                        ModelState.AddModelError(nameof(model.BankName), "اسم البنك مطلوب");
                    if (string.IsNullOrWhiteSpace(model.BankAccountName))
                        ModelState.AddModelError(nameof(model.BankAccountName), "اسم صاحب الحساب مطلوب");
                    if (string.IsNullOrWhiteSpace(model.BankAccountNumber))
                        ModelState.AddModelError(nameof(model.BankAccountNumber), "رقم الحساب البنكي مطلوب");
                    
                    // Validate SWIFT code if provided
                    if (!string.IsNullOrWhiteSpace(model.SwiftCode) && (model.SwiftCode.Length < 8 || model.SwiftCode.Length > 11))
                    {
                        ModelState.AddModelError(nameof(model.SwiftCode), "رمز SWIFT يجب أن يكون بين 8 و 11 حرف");
                    }
                }
                else if (model.PayoutMethod == "VodafoneCash" || 
                         model.PayoutMethod == "InstaPay" || 
                         model.PayoutMethod == "OrangeMoney" || 
                         model.PayoutMethod == "EtisalatCash")
                {
                    if (string.IsNullOrWhiteSpace(model.MobileWalletNumber))
                    {
                        ModelState.AddModelError(nameof(model.MobileWalletNumber), "رقم المحفظة الإلكترونية مطلوب");
                    }
                    else
                    {
                        // Clean the phone number using centralized helper method
                        var cleanedPhone = BusinessRuleHelper.CleanMobileWalletNumber(model.MobileWalletNumber);
                        
                        // Validate using centralized helper method
                        var validationResult = BusinessRuleHelper.ValidateMobileWalletNumber(cleanedPhone, model.MobileWalletProvider ?? string.Empty);
                        
                        if (!validationResult.IsValid)
                        {
                            ModelState.AddModelError(nameof(model.MobileWalletNumber), validationResult.Reason ?? "رقم المحفظة غير صحيح");
                        }
                        else
                        {
                            // Update the model with cleaned value for consistency
                            model.MobileWalletNumber = cleanedPhone;
                        }
                    }
                    
                    // Validate provider is selected
                    if (string.IsNullOrWhiteSpace(model.MobileWalletProvider))
                    {
                        ModelState.AddModelError(nameof(model.MobileWalletProvider), "يجب اختيار مزود المحفظة");
                    }
                }
            }

            if (!ModelState.IsValid)
            {
                try { await PopulatePaymentDropdowns(); } catch (Exception ex) { _logger.LogError(ex, "Error populating payment dropdowns"); }
                await SetDefaultCurrencyAsync();
                return View(model);
            }

            var dto = new UpdatePaymentSettingsDto
            {
                PayoutMethod = model.PayoutMethod ?? string.Empty,
                PayPalEmail = model.PayPalEmail?.Trim(),
                BankName = model.BankName?.Trim(),
                BankAccountName = model.BankAccountName?.Trim(),
                BankAccountNumber = model.BankAccountNumber?.Trim(),
                IBAN = model.IBAN?.Trim().Replace(" ", "").ToUpper(),
                SwiftCode = model.SwiftCode?.Trim().ToUpper(),
                // Clean mobile wallet number using centralized helper to ensure consistency
                MobileWalletNumber = !string.IsNullOrWhiteSpace(model.MobileWalletNumber) 
                    ? BusinessRuleHelper.CleanMobileWalletNumber(model.MobileWalletNumber)
                    : null,
                MobileWalletProvider = model.MobileWalletProvider?.Trim(),
                WiseEmail = model.WiseEmail?.Trim(),
                StripeAccountId = model.StripeAccountId?.Trim()
            };

            var result = await _profileService.UpdatePaymentSettingsAsync(userId, dto);

            if (result.IsSuccess)
            {
                _logger.LogInformation("Payment settings updated successfully for userId {UserId}", userId);
                SetSuccessMessage("تم حفظ إعدادات الدفع بنجاح");
                return RedirectToAction(nameof(PaymentSettings));
            }

            _logger.LogWarning("Payment settings update failed for userId {UserId}: {Error}", userId, result.Error);
            SetErrorMessage(result.Error ?? "حدث خطأ أثناء حفظ إعدادات الدفع");
            try { await PopulatePaymentDropdowns(); } catch (Exception ex) { _logger.LogError(ex, "Error populating payment dropdowns after failed update"); }
            await SetDefaultCurrencyAsync();
            return View(model);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in PaymentSettings POST for userId {UserId}. Message: {Message}, Inner: {Inner}",
                _currentUserService.UserId, ex.Message, ex.InnerException?.Message);
            SetErrorMessage("حدث خطأ غير متوقع أثناء حفظ إعدادات الدفع");
            try { await PopulatePaymentDropdowns(); } catch { /* ignore */ }
            await SetDefaultCurrencyAsync();
            return View(model);
        }
    }

    #endregion

    #region Social Links

    /// <summary>
    /// روابط التواصل - Social Links (GET)
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> SocialLinks()
    {
        try
        {
            var userId = _currentUserService.UserId;
            if (string.IsNullOrEmpty(userId))
            {
                _logger.LogWarning("SocialLinks GET: UserId is null or empty");
                return RedirectToAction("Login", "Account", new { area = "" });
            }

            var socialLinks = await _profileService.GetSocialLinksAsync(userId);
            if (socialLinks == null)
            {
                _logger.LogWarning("SocialLinks GET: Social links not found for userId {UserId}", userId);
                // Return empty view model instead of NotFound
                socialLinks = new InstructorSocialLinksDto();
            }

            var viewModel = new InstructorSocialLinksViewModel
            {
                Website = socialLinks.Website,
                FacebookUrl = socialLinks.FacebookUrl,
                TwitterUrl = socialLinks.TwitterUrl,
                LinkedInUrl = socialLinks.LinkedInUrl,
                YouTubeUrl = socialLinks.YouTubeUrl,
                InstagramUrl = socialLinks.InstagramUrl,
                GitHubUrl = socialLinks.GitHubUrl,
                TikTokUrl = socialLinks.TikTokUrl
            };

            try
            {
                ViewBag.SocialPlatforms = await _dropdownService.GetSocialPlatformsAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading social platforms");
                ViewBag.SocialPlatforms = new List<object>();
            }

            return View(viewModel);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in SocialLinks GET for userId {UserId}", _currentUserService.UserId);
            SetErrorMessage("حدث خطأ أثناء تحميل روابط التواصل");
            return RedirectToAction(nameof(Index));
        }
    }

    /// <summary>
    /// حفظ روابط التواصل - Save Social Links (POST)
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SocialLinks(InstructorSocialLinksViewModel model)
    {
        try
        {
            var userId = _currentUserService.UserId;
            if (string.IsNullOrEmpty(userId))
            {
                _logger.LogWarning("SocialLinks POST: UserId is null or empty");
                return RedirectToAction("Login", "Account", new { area = "" });
            }

            // Additional URL validation
            var urlFields = new Dictionary<string, string?>
            {
                { nameof(model.Website), model.Website },
                { nameof(model.FacebookUrl), model.FacebookUrl },
                { nameof(model.TwitterUrl), model.TwitterUrl },
                { nameof(model.LinkedInUrl), model.LinkedInUrl },
                { nameof(model.YouTubeUrl), model.YouTubeUrl },
                { nameof(model.InstagramUrl), model.InstagramUrl },
                { nameof(model.GitHubUrl), model.GitHubUrl },
                { nameof(model.TikTokUrl), model.TikTokUrl }
            };

            foreach (var (fieldName, url) in urlFields)
            {
                if (!string.IsNullOrWhiteSpace(url))
                {
                    if (!Uri.TryCreate(url, UriKind.Absolute, out var uriResult) ||
                        (uriResult.Scheme != Uri.UriSchemeHttp && uriResult.Scheme != Uri.UriSchemeHttps))
                    {
                        ModelState.AddModelError(fieldName, "الرابط غير صحيح. يجب أن يبدأ بـ http:// أو https://");
                    }
                }
            }

            if (!ModelState.IsValid)
            {
                try
                {
                    ViewBag.SocialPlatforms = await _dropdownService.GetSocialPlatformsAsync();
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error loading social platforms");
                }
                return View(model);
            }

            var dto = new UpdateSocialLinksDto
            {
                Website = model.Website?.Trim(),
                FacebookUrl = model.FacebookUrl?.Trim(),
                TwitterUrl = model.TwitterUrl?.Trim(),
                LinkedInUrl = model.LinkedInUrl?.Trim(),
                YouTubeUrl = model.YouTubeUrl?.Trim(),
                InstagramUrl = model.InstagramUrl?.Trim(),
                GitHubUrl = model.GitHubUrl?.Trim(),
                TikTokUrl = model.TikTokUrl?.Trim()
            };

            var result = await _profileService.UpdateSocialLinksAsync(userId, dto);

            if (result.IsSuccess)
            {
                _logger.LogInformation("Social links updated successfully for userId {UserId}", userId);
                SetSuccessMessage("تم حفظ روابط التواصل بنجاح");
                return RedirectToAction(nameof(SocialLinks));
            }

            _logger.LogWarning("Social links update failed for userId {UserId}: {Error}", userId, result.Error);
            SetErrorMessage(result.Error ?? "حدث خطأ أثناء حفظ روابط التواصل");
            
            try
            {
                ViewBag.SocialPlatforms = await _dropdownService.GetSocialPlatformsAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading social platforms after failed update");
            }
            
            return View(model);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in SocialLinks POST for userId {UserId}. Message: {Message}, Inner: {Inner}",
                _currentUserService.UserId, ex.Message, ex.InnerException?.Message);
            SetErrorMessage("حدث خطأ غير متوقع أثناء حفظ روابط التواصل");
            
            try
            {
                ViewBag.SocialPlatforms = await _dropdownService.GetSocialPlatformsAsync();
            }
            catch
            {
                // Ignore
            }
            
            return View(model);
        }
    }

    #endregion

    #region Notification Preferences

    /// <summary>
    /// تفضيلات الإشعارات - Notification Preferences (GET)
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> Notifications()
    {
        try
        {
            var userId = _currentUserService.UserId;
            if (string.IsNullOrEmpty(userId))
            {
                _logger.LogWarning("Notifications GET: UserId is null or empty");
                return RedirectToAction("Login", "Account", new { area = "" });
            }

            var prefs = await _profileService.GetNotificationPreferencesAsync(userId);
            
            var viewModel = new InstructorNotificationPreferencesViewModel
            {
                EmailOnNewEnrollment = prefs?.EmailOnNewEnrollment ?? true,
                EmailOnNewReview = prefs?.EmailOnNewReview ?? true,
                EmailOnNewQuestion = prefs?.EmailOnNewQuestion ?? true,
                EmailOnNewMessage = prefs?.EmailOnNewMessage ?? true,
                EmailOnPaymentReceived = prefs?.EmailOnPaymentReceived ?? true,
                EmailOnWithdrawalProcessed = prefs?.EmailOnWithdrawalProcessed ?? true,
                EmailWeeklyDigest = prefs?.EmailWeeklyDigest ?? true,
                EmailMonthlyReport = prefs?.EmailMonthlyReport ?? true,
                EmailMarketingUpdates = prefs?.EmailMarketingUpdates ?? false,
                PushNotifications = prefs?.PushNotifications ?? true,
                InAppNotifications = prefs?.InAppNotifications ?? true
            };

            return View(viewModel);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in Notifications GET for userId {UserId}", _currentUserService.UserId);
            SetErrorMessage("حدث خطأ أثناء تحميل تفضيلات الإشعارات");
            return RedirectToAction(nameof(Index));
        }
    }

    /// <summary>
    /// حفظ تفضيلات الإشعارات - Save Notification Preferences (POST)
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Notifications(InstructorNotificationPreferencesViewModel model)
    {
        try
        {
            var userId = _currentUserService.UserId;
            if (string.IsNullOrEmpty(userId))
            {
                _logger.LogWarning("Notifications POST: UserId is null or empty");
                return RedirectToAction("Login", "Account", new { area = "" });
            }

            var dto = new UpdateNotificationPreferencesDto
            {
                EmailOnNewEnrollment = model.EmailOnNewEnrollment,
                EmailOnNewReview = model.EmailOnNewReview,
                EmailOnNewQuestion = model.EmailOnNewQuestion,
                EmailOnNewMessage = model.EmailOnNewMessage,
                EmailOnPaymentReceived = model.EmailOnPaymentReceived,
                EmailOnWithdrawalProcessed = model.EmailOnWithdrawalProcessed,
                EmailWeeklyDigest = model.EmailWeeklyDigest,
                EmailMonthlyReport = model.EmailMonthlyReport,
                EmailMarketingUpdates = model.EmailMarketingUpdates,
                PushNotifications = model.PushNotifications,
                InAppNotifications = model.InAppNotifications
            };

            var result = await _profileService.UpdateNotificationPreferencesAsync(userId, dto);

            if (result.IsSuccess)
            {
                _logger.LogInformation("Notification preferences updated successfully for userId {UserId}", userId);
                SetSuccessMessage("تم حفظ تفضيلات الإشعارات بنجاح");
                return RedirectToAction(nameof(Notifications));
            }

            _logger.LogWarning("Notification preferences update failed for userId {UserId}: {Error}", userId, result.Error);
            SetErrorMessage(result.Error ?? "حدث خطأ أثناء حفظ تفضيلات الإشعارات");
            return View(model);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in Notifications POST for userId {UserId}. Message: {Message}, Inner: {Inner}",
                _currentUserService.UserId, ex.Message, ex.InnerException?.Message);
            SetErrorMessage("حدث خطأ غير متوقع أثناء حفظ تفضيلات الإشعارات");
            return View(model);
        }
    }

    #endregion

    #region Security

    /// <summary>
    /// إعدادات الأمان - Security Settings (GET)
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> Security()
    {
        try
        {
            var userId = _currentUserService.UserId;
            if (string.IsNullOrEmpty(userId))
            {
                _logger.LogWarning("Security GET: UserId is null or empty");
                return RedirectToAction("Login", "Account", new { area = "" });
            }

            var security = await _profileService.GetSecuritySettingsAsync(userId);
            if (security == null)
            {
                _logger.LogWarning("Security GET: Security settings not found for userId {UserId}", userId);
                SetErrorMessage("تعذر تحميل إعدادات الأمان");
                return RedirectToAction(nameof(Index));
            }

            List<LoginHistoryDto> loginHistory;
            try
            {
                loginHistory = await _profileService.GetLoginHistoryAsync(userId, 20);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading login history for userId {UserId}", userId);
                loginHistory = new List<LoginHistoryDto>();
            }

            var viewModel = new InstructorSecurityViewModel
            {
                TwoFactorEnabled = security.TwoFactorEnabled,
                IsEmailVerified = security.IsEmailVerified,
                IsPhoneVerified = security.IsPhoneVerified,
                LastLoginDate = security.LastLoginDate,
                LastLoginIp = security.LastLoginIp,
                ActiveSessionsCount = security.ActiveSessionsCount,
                LoginHistory = loginHistory.Select(l => new LoginHistoryItemViewModel
                {
                    LoginTime = l.LoginTime,
                    IpAddress = l.IpAddress,
                    Location = l.Location,
                    DeviceType = l.DeviceType,
                    Browser = l.Browser,
                    IsSuccessful = l.IsSuccessful
                }).ToList()
            };

            return View(viewModel);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in Security GET for userId {UserId}", _currentUserService.UserId);
            SetErrorMessage("حدث خطأ أثناء تحميل إعدادات الأمان");
            return RedirectToAction(nameof(Index));
        }
    }

    /// <summary>
    /// تغيير كلمة المرور - Change Password (POST)
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ChangePassword(ChangePasswordViewModel model)
    {
        try
        {
            var userId = _currentUserService.UserId;
            if (string.IsNullOrEmpty(userId))
            {
                _logger.LogWarning("ChangePassword POST: UserId is null or empty");
                return RedirectToAction("Login", "Account", new { area = "" });
            }

            // Additional password strength validation
            if (!string.IsNullOrWhiteSpace(model.NewPassword))
            {
                if (model.NewPassword.Length < 8)
                {
                    ModelState.AddModelError(nameof(model.NewPassword), "كلمة المرور يجب أن تكون 8 أحرف على الأقل");
                }
                else if (!System.Text.RegularExpressions.Regex.IsMatch(model.NewPassword, @"^(?=.*[a-z])(?=.*[A-Z])(?=.*\d)"))
                {
                    ModelState.AddModelError(nameof(model.NewPassword), "كلمة المرور يجب أن تحتوي على حرف كبير وصغير ورقم على الأقل");
                }
            }

            if (!ModelState.IsValid)
            {
                SetErrorMessage("يرجى التحقق من البيانات المدخلة");
                return RedirectToAction(nameof(Security));
            }

            var dto = new ChangePasswordDto
            {
                CurrentPassword = model.CurrentPassword,
                NewPassword = model.NewPassword,
                ConfirmPassword = model.ConfirmPassword,
                LogoutOtherSessions = model.LogoutOtherSessions
            };

            var result = await _profileService.ChangePasswordAsync(userId, dto);

            if (result.IsSuccess)
            {
                _logger.LogInformation("Password changed successfully for userId {UserId}", userId);
                SetSuccessMessage("تم تغيير كلمة المرور بنجاح");
            }
            else
            {
                _logger.LogWarning("Password change failed for userId {UserId}: {Error}", userId, result.Error);
                SetErrorMessage(result.Error ?? "حدث خطأ أثناء تغيير كلمة المرور");
            }

            return RedirectToAction(nameof(Security));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in ChangePassword POST for userId {UserId}", _currentUserService.UserId);
            SetErrorMessage("حدث خطأ غير متوقع أثناء تغيير كلمة المرور");
            return RedirectToAction(nameof(Security));
        }
    }

    /// <summary>
    /// تفعيل المصادقة الثنائية - Enable Two-Factor (POST)
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> EnableTwoFactor()
    {
        var userId = _currentUserService.UserId;
        if (string.IsNullOrEmpty(userId))
            return RedirectToAction("Login", "Account", new { area = "" });

        var result = await _profileService.EnableTwoFactorAsync(userId);

        if (result.IsSuccess)
        {
            TempData["TwoFactorSetup"] = System.Text.Json.JsonSerializer.Serialize(result.Data);
            return RedirectToAction("TwoFactorSetup");
        }

        SetErrorMessage(result.Error ?? "حدث خطأ أثناء تفعيل المصادقة الثنائية");
        return RedirectToAction(nameof(Security));
    }

    /// <summary>
    /// صفحة إعداد المصادقة الثنائية - Two-Factor Setup Page
    /// </summary>
    [HttpGet]
    public IActionResult TwoFactorSetup()
    {
        try
        {
            var setupData = TempData["TwoFactorSetup"] as string;
            if (string.IsNullOrEmpty(setupData))
            {
                _logger.LogWarning("TwoFactorSetup GET: Setup data is missing");
                SetErrorMessage("انتهت صلاحية بيانات الإعداد. يرجى المحاولة مرة أخرى");
                return RedirectToAction(nameof(Security));
            }

            TwoFactorSetupDto? setup;
            try
            {
                setup = System.Text.Json.JsonSerializer.Deserialize<TwoFactorSetupDto>(setupData);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deserializing TwoFactor setup data");
                SetErrorMessage("حدث خطأ أثناء تحميل بيانات الإعداد");
                return RedirectToAction(nameof(Security));
            }

            if (setup == null)
            {
                SetErrorMessage("بيانات الإعداد غير صحيحة");
                return RedirectToAction(nameof(Security));
            }

            return View(setup);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in TwoFactorSetup GET");
            SetErrorMessage("حدث خطأ أثناء تحميل صفحة الإعداد");
            return RedirectToAction(nameof(Security));
        }
    }

    /// <summary>
    /// التحقق من المصادقة الثنائية - Verify Two-Factor (POST)
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> VerifyTwoFactor(string secretKey, string verificationCode)
    {
        try
        {
            var userId = _currentUserService.UserId;
            if (string.IsNullOrEmpty(userId))
            {
                _logger.LogWarning("VerifyTwoFactor POST: UserId is null or empty");
                return RedirectToAction("Login", "Account", new { area = "" });
            }

            if (string.IsNullOrWhiteSpace(verificationCode) || verificationCode.Length != 6)
            {
                SetErrorMessage("رمز التحقق يجب أن يكون 6 أرقام");
                return RedirectToAction(nameof(Security));
            }

            var result = await _profileService.VerifyAndEnableTwoFactorAsync(userId, verificationCode.Trim());

            if (result.IsSuccess)
            {
                SetSuccessMessage("تم تفعيل المصادقة الثنائية بنجاح");
                return RedirectToAction(nameof(Security));
            }

            SetErrorMessage(result.Error ?? "حدث خطأ أثناء التحقق من رمز المصادقة");
            return RedirectToAction(nameof(Security));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in VerifyTwoFactor POST for userId {UserId}", _currentUserService.UserId);
            SetErrorMessage("حدث خطأ أثناء التحقق من رمز المصادقة");
            return RedirectToAction(nameof(Security));
        }
    }

    #endregion

    #region Activity

    /// <summary>
    /// سجل النشاط - Activity Log
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> Activity(int page = 1)
    {
        try
        {
            var userId = _currentUserService.UserId;
            if (string.IsNullOrEmpty(userId))
            {
                _logger.LogWarning("Activity GET: UserId is null or empty");
                return RedirectToAction("Login", "Account", new { area = "" });
            }

            // Validate page number
            if (page < 1) page = 1;

            var activityLog = await _profileService.GetActivityLogAsync(userId, page, 20);

            if (activityLog == null)
            {
                _logger.LogWarning("Activity GET: Activity log is null for userId {UserId}", userId);
                activityLog = new InstructorPagedResult<ActivityLogDto>
                {
                    Items = new List<ActivityLogDto>(),
                    TotalCount = 0,
                    Page = page,
                    PageSize = 20
                };
            }

            return View(activityLog);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in Activity GET for userId {UserId}. Message: {Message}, Inner: {Inner}",
                _currentUserService.UserId, ex.Message, ex.InnerException?.Message);
            SetErrorMessage("حدث خطأ أثناء تحميل سجل النشاط");
            return RedirectToAction(nameof(Index));
        }
    }

    #endregion

    #region Helper Methods

    private async Task PopulateEditDropdowns()
    {
        try
        {
            ViewBag.Countries = await _dropdownService.GetCountriesAsync() ?? new List<SelectListItem>();
            ViewBag.Timezones = await _dropdownService.GetTimezonesAsync() ?? new List<SelectListItem>();
            ViewBag.Languages = await _dropdownService.GetUILanguagesAsync() ?? new List<SelectListItem>();
            ViewBag.Specializations = await _dropdownService.GetSpecializationsAsync() ?? new List<SelectListItem>();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error populating edit dropdowns");
            ViewBag.Countries = new List<object>();
            ViewBag.Timezones = new List<object>();
            ViewBag.Languages = new List<object>();
            ViewBag.Specializations = new List<object>();
        }
    }

    private async Task PopulateSettingsDropdowns()
    {
        await PopulateEditDropdowns();
        await PopulatePaymentDropdowns();
    }

    private async Task PopulatePaymentDropdowns()
    {
        try
        {
            ViewBag.PaymentMethods = await _dropdownService.GetPaymentMethodsAsync() ?? new List<SelectListItem>();
            ViewBag.Banks = await _dropdownService.GetBanksAsync("EG") ?? new List<SelectListItem>();
            ViewBag.MobileWalletProviders = await _dropdownService.GetMobileWalletProvidersAsync("EG") ?? new List<SelectListItem>();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error populating payment dropdowns");
            ViewBag.PaymentMethods = new List<object>();
            ViewBag.Banks = new List<object>();
            ViewBag.MobileWalletProviders = new List<object>();
        }
    }

    #endregion
}
