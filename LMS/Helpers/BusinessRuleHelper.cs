using LMS.Domain.Entities.Financial;
using LMS.Domain.Entities.Books;
using LMS.Domain.Enums;

namespace LMS.Helpers;

/// <summary>
/// مساعد قواعد العمل - Business Rules Helper
/// </summary>
public static class BusinessRuleHelper
{
    /// <summary>
    /// الحد الأدنى لسعر الدورة - Minimum course price
    /// </summary>
    public const decimal MinimumCoursePrice = 0.01m;

    /// <summary>
    /// الحد الأقصى لسعر الدورة - Maximum course price
    /// </summary>
    public const decimal MaximumCoursePrice = 999999.99m;

    /// <summary>
    /// الحد الأدنى لمعدل العمولة - Minimum commission rate
    /// </summary>
    public const decimal MinimumCommissionRate = 10m;

    /// <summary>
    /// الحد الأقصى لمعدل العمولة - Maximum commission rate
    /// </summary>
    public const decimal MaximumCommissionRate = 95m;

    /// <summary>
    /// الحد الأدنى للسحب - Minimum withdrawal amount
    /// </summary>
    public const decimal MinimumWithdrawalAmount = 50m;

    /// <summary>
    /// فترة الاحتفاظ بالأرباح (أيام) - Earnings hold period
    /// </summary>
    public const int EarningsHoldPeriodDays = 14;

    /// <summary>
    /// الحد الأقصى لمحاولات الاختبار - Maximum quiz attempts
    /// </summary>
    public const int MaxQuizAttempts = 10;

    /// <summary>
    /// مدة انتهاء صلاحية الكوبون الافتراضية (أيام) - Default coupon expiry days
    /// </summary>
    public const int DefaultCouponExpiryDays = 30;

    /// <summary>
    /// الحد الأدنى لدرجة النجاح - Minimum passing score
    /// </summary>
    public const decimal MinimumPassingScore = 50m;

    /// <summary>
    /// الحد الأدنى لعدد الدروس في الدورة - Minimum lessons per course
    /// </summary>
    public const int MinimumLessonsPerCourse = 3;

    /// <summary>
    /// الحد الأدنى لطول وصف الدورة - Minimum course description length
    /// </summary>
    public const int MinimumCourseDescriptionLength = 100;

    /// <summary>
    /// الحد الأدنى لطول الوصف المختصر - Minimum short description length
    /// </summary>
    public const int MinimumShortDescriptionLength = 20;

    /// <summary>
    /// الحد الأدنى لعدد مخرجات التعلم - Minimum learning outcomes count
    /// </summary>
    public const int MinimumLearningOutcomesCount = 3;

    /// <summary>
    /// الحد الأدنى لعدد متطلبات الدورة - Minimum course requirements count
    /// </summary>
    public const int MinimumCourseRequirementsCount = 1;

    /// <summary>
    /// فترة تحديث المحتوى (أشهر) - Content update threshold in months
    /// </summary>
    public const int ContentUpdateThresholdMonths = 6;

    /// <summary>
    /// التحقق من إمكانية حذف الدورة - Check if course can be deleted
    /// </summary>
    public static (bool CanDelete, string? Reason) CanDeleteCourse(int enrollmentCount, CourseStatus status)
    {
        if (enrollmentCount > 0)
            return (false, "لا يمكن حذف الدورة لأنها تحتوي على تسجيلات");

        if (status == CourseStatus.Published)
            return (false, "لا يمكن حذف دورة منشورة. قم بتعليقها أولاً");

        return (true, null);
    }

    /// <summary>
    /// التحقق من إمكانية نشر الدورة - Check if course can be published
    /// </summary>
    public static (bool CanPublish, string? Reason) CanPublishCourse(
        int moduleCount, 
        int lessonCount, 
        bool hasThumbnail, 
        bool hasPrice, 
        bool isFree)
    {
        if (moduleCount == 0)
            return (false, "يجب إضافة محتوى للدورة قبل النشر");

        if (lessonCount == 0)
            return (false, "يجب إضافة دروس للدورة قبل النشر");

        if (!hasThumbnail)
            return (false, "يجب إضافة صورة للدورة قبل النشر");

        if (!isFree && !hasPrice)
            return (false, "يجب تحديد سعر للدورة أو جعلها مجانية");

        return (true, null);
    }

    /// <summary>
    /// التحقق من إمكانية حذف الكوبون - Check if coupon can be deleted
    /// </summary>
    public static (bool CanDelete, string? Reason) CanDeleteCoupon(int usageCount)
    {
        if (usageCount > 0)
            return (false, "لا يمكن حذف الكوبون لأنه تم استخدامه من قبل");

        return (true, null);
    }

    /// <summary>
    /// التحقق من إمكانية الاسترداد - Check if refund is possible
    /// </summary>
    public static (bool CanRefund, string? Reason) CanRefund(
        DateTime enrollmentDate, 
        int refundWindowDays, 
        decimal progressPercentage)
    {
        var daysSinceEnrollment = (DateTime.UtcNow - enrollmentDate).TotalDays;

        if (daysSinceEnrollment > refundWindowDays)
            return (false, $"انتهت فترة الاسترداد ({refundWindowDays} يوم)");

        if (progressPercentage > 30)
            return (false, "لا يمكن الاسترداد بعد إكمال أكثر من 30% من الدورة");

        return (true, null);
    }

    /// <summary>
    /// حساب العمولة - Calculate commission
    /// </summary>
    public static (decimal PlatformCommission, decimal InstructorAmount) CalculateCommission(
        decimal totalAmount, 
        decimal instructorCommissionRate)
    {
        var platformRate = 100 - instructorCommissionRate;
        var platformCommission = totalAmount * (platformRate / 100);
        var instructorAmount = totalAmount - platformCommission;

        return (platformCommission, instructorAmount);
    }

    /// <summary>
    /// التحقق من صلاحية الكوبون - Validate coupon
    /// </summary>
    public static (bool IsValid, string? Reason) ValidateCouponDates(
        DateTime validFrom, 
        DateTime validTo, 
        DateTime? currentDate = null)
    {
        currentDate ??= DateTime.UtcNow;

        if (validFrom >= validTo)
            return (false, "تاريخ الانتهاء يجب أن يكون بعد تاريخ البدء");

        if (validTo < currentDate)
            return (false, "انتهت صلاحية الكوبون");

        if (validFrom > currentDate)
            return (false, "الكوبون غير نشط بعد");

        return (true, null);
    }

    /// <summary>
    /// التحقق من حدود السحب - Validate withdrawal limits
    /// </summary>
    public static (bool IsValid, string? Reason) ValidateWithdrawal(
        decimal requestedAmount, 
        decimal availableBalance, 
        decimal minimumWithdrawal,
        DateTime? lastWithdrawalDate = null)
    {
        if (requestedAmount < minimumWithdrawal)
            return (false, $"الحد الأدنى للسحب هو {minimumWithdrawal}");

        if (requestedAmount > availableBalance)
            return (false, "المبلغ المطلوب أكبر من الرصيد المتاح");

        if (lastWithdrawalDate.HasValue)
        {
            var daysSinceLastWithdrawal = (DateTime.UtcNow - lastWithdrawalDate.Value).TotalDays;
            if (daysSinceLastWithdrawal < 7)
                return (false, "يمكنك طلب السحب مرة واحدة كل أسبوع");
        }

        return (true, null);
    }

    /// <summary>
    /// حساب تاريخ توفر الأرباح - Calculate earnings availability date
    /// </summary>
    public static DateTime CalculateEarningsAvailabilityDate(DateTime purchaseDate)
    {
        return purchaseDate.AddDays(EarningsHoldPeriodDays);
    }

    /// <summary>
    /// التحقق من صلاحية التقييم - Validate review rating
    /// </summary>
    public static (bool IsValid, string? Reason) ValidateReviewRating(int rating)
    {
        if (rating < 1 || rating > 5)
            return (false, "التقييم يجب أن يكون بين 1 و 5");

        return (true, null);
    }

    /// <summary>
    /// التحقق من إمكانية حذف الوحدة - Check if module can be deleted
    /// </summary>
    public static (bool CanDelete, string? Reason) CanDeleteModule(int lessonCount, bool isPublished)
    {
        if (lessonCount > 0)
            return (false, "لا يمكن حذف الوحدة لأنها تحتوي على دروس. قم بحذف الدروس أولاً");

        if (isPublished)
            return (false, "لا يمكن حذف وحدة منشورة. قم بإلغاء نشرها أولاً");

        return (true, null);
    }

    /// <summary>
    /// التحقق من إمكانية حذف الدرس - Check if lesson can be deleted
    /// </summary>
    public static (bool CanDelete, string? Reason) CanDeleteLesson(
        bool hasStudentProgress,
        int enrollmentCount,
        bool isPublished)
    {
        if (hasStudentProgress && enrollmentCount > 0)
            return (false, "لا يمكن حذف الدرس لأنه يحتوي على تقدم للطلاب");

        if (isPublished && enrollmentCount > 5)
            return (false, "لا يمكن حذف درس منشور في دورة بها أكثر من 5 تسجيلات");

        return (true, null);
    }

    /// <summary>
    /// التحقق من صلاحية معلومات الاختبار - Validate quiz settings
    /// </summary>
    public static (bool IsValid, string? Reason) ValidateQuizSettings(
        decimal passingScore,
        int? timeLimitMinutes,
        int? maxAttempts,
        int questionCount)
    {
        if (questionCount == 0)
            return (false, "يجب إضافة أسئلة للاختبار قبل النشر");

        if (passingScore < 0 || passingScore > 100)
            return (false, "درجة النجاح يجب أن تكون بين 0 و 100");

        if (timeLimitMinutes.HasValue && timeLimitMinutes.Value < 1)
            return (false, "الوقت المحدد يجب أن يكون دقيقة واحدة على الأقل");

        if (maxAttempts.HasValue && maxAttempts.Value < 1)
            return (false, "عدد المحاولات يجب أن يكون 1 على الأقل");

        if (maxAttempts.HasValue && maxAttempts.Value > MaxQuizAttempts)
            return (false, $"الحد الأقصى لعدد المحاولات هو {MaxQuizAttempts}");

        return (true, null);
    }

    /// <summary>
    /// التحقق من صلاحية معلومات التكليف - Validate assignment settings
    /// </summary>
    public static (bool IsValid, string? Reason) ValidateAssignmentSettings(
        decimal maxPoints,
        DateTime? dueDate,
        int? maxFileSize,
        int? maxFiles)
    {
        if (maxPoints <= 0)
            return (false, "النقاط يجب أن تكون أكبر من صفر");

        if (maxPoints > 1000)
            return (false, "الحد الأقصى للنقاط هو 1000");

        if (dueDate.HasValue && dueDate.Value < DateTime.UtcNow)
            return (false, "تاريخ التسليم يجب أن يكون في المستقبل");

        if (maxFileSize.HasValue && maxFileSize.Value > 100)
            return (false, "الحد الأقصى لحجم الملف هو 100 ميجابايت");

        if (maxFiles.HasValue && (maxFiles.Value < 1 || maxFiles.Value > 10))
            return (false, "عدد الملفات يجب أن يكون بين 1 و 10");

        return (true, null);
    }

    /// <summary>
    /// التحقق من صلاحية معلومات التكليف - Validate assignment settings (overload)
    /// </summary>
    public static (bool IsValid, string? Reason) ValidateAssignmentSettings(
        string title,
        decimal maxGrade,
        DateTime? dueDate,
        bool allowLateSubmissions,
        decimal latePenaltyPercentage)
    {
        if (string.IsNullOrWhiteSpace(title) || title.Length < 3)
            return (false, "عنوان التكليف يجب أن يكون 3 أحرف على الأقل");

        if (title.Length > 300)
            return (false, "عنوان التكليف يجب ألا يتجاوز 300 حرف");

        if (maxGrade <= 0)
            return (false, "الدرجة القصوى يجب أن تكون أكبر من صفر");

        if (maxGrade > 1000)
            return (false, "الحد الأقصى للدرجة هو 1000");

        if (dueDate.HasValue && dueDate.Value < DateTime.UtcNow)
            return (false, "تاريخ التسليم يجب أن يكون في المستقبل");

        if (allowLateSubmissions && (latePenaltyPercentage < 0 || latePenaltyPercentage > 100))
            return (false, "نسبة خصم التأخير يجب أن تكون بين 0 و 100");

        return (true, null);
    }

    /// <summary>
    /// التحقق من صلاحية تقييم التكليف - Validate assignment grading
    /// </summary>
    public static (bool IsValid, string? Reason) ValidateAssignmentGrade(
        decimal? grade,
        decimal maxPoints,
        AssignmentStatus status)
    {
        if (grade.HasValue && grade.Value < 0)
            return (false, "الدرجة يجب أن تكون صفر أو أكبر");

        if (grade.HasValue && grade.Value > maxPoints)
            return (false, $"الدرجة لا يمكن أن تكون أكبر من {maxPoints}");

        if (status == AssignmentStatus.Graded && !grade.HasValue)
            return (false, "يجب تحديد الدرجة عند تغيير الحالة إلى مكتمل");

        return (true, null);
    }

    /// <summary>
    /// التحقق من صلاحية الكوبون - Comprehensive coupon validation
    /// </summary>
    public static (bool IsValid, string? Reason) ValidateCoupon(
        DiscountType discountType,
        decimal discountValue,
        decimal? maxDiscountAmount,
        decimal? minimumPurchaseAmount,
        DateTime validFrom,
        DateTime validTo,
        int? maxUses,
        int? maxUsesPerUser)
    {
        // Validate discount value
        if (discountType == DiscountType.Percentage)
        {
            if (discountValue <= 0 || discountValue > 100)
                return (false, "نسبة الخصم يجب أن تكون بين 0 و 100");
        }
        else if (discountType == DiscountType.FixedAmount)
        {
            if (discountValue <= 0)
                return (false, "قيمة الخصم يجب أن تكون أكبر من صفر");
        }

        // Validate dates
        if (validFrom >= validTo)
            return (false, "تاريخ الانتهاء يجب أن يكون بعد تاريخ البدء");

        // Validate minimum purchase
        if (minimumPurchaseAmount.HasValue && minimumPurchaseAmount.Value < 0)
            return (false, "الحد الأدنى للشراء يجب أن يكون صفر أو أكبر");

        // Validate maximum discount
        if (maxDiscountAmount.HasValue && maxDiscountAmount.Value <= 0)
            return (false, "الحد الأقصى للخصم يجب أن يكون أكبر من صفر");

        // Validate usage limits
        if (maxUses.HasValue && maxUses.Value < 1)
            return (false, "الحد الأقصى للاستخدامات يجب أن يكون 1 على الأقل");

        if (maxUsesPerUser.HasValue && maxUsesPerUser.Value < 1)
            return (false, "الحد الأقصى للاستخدامات لكل مستخدم يجب أن يكون 1 على الأقل");

        if (maxUsesPerUser.HasValue && maxUses.HasValue && maxUsesPerUser.Value > maxUses.Value)
            return (false, "الحد الأقصى للاستخدامات لكل مستخدم لا يمكن أن يكون أكبر من الحد الأقصى الإجمالي");

        return (true, null);
    }

    /// <summary>
    /// التحقق من صلاحية معلومات الحصة المباشرة - Validate live class settings
    /// </summary>
    public static (bool IsValid, string? Reason) ValidateLiveClass(
        DateTime scheduledStartTime,
        DateTime scheduledEndTime,
        int? maxParticipants,
        int? reminderMinutesBefore)
    {
        if (scheduledStartTime >= scheduledEndTime)
            return (false, "وقت الانتهاء يجب أن يكون بعد وقت البداية");

        if (scheduledStartTime < DateTime.UtcNow.AddMinutes(30))
            return (false, "لا يمكن جدولة حصة مباشرة في أقل من 30 دقيقة");

        var duration = (scheduledEndTime - scheduledStartTime).TotalHours;
        if (duration > 8)
            return (false, "الحد الأقصى لمدة الحصة هو 8 ساعات");

        if (duration < 0.25) // 15 minutes
            return (false, "الحد الأدنى لمدة الحصة هو 15 دقيقة");

        if (maxParticipants.HasValue && maxParticipants.Value < 1)
            return (false, "عدد المشاركين يجب أن يكون 1 على الأقل");

        if (reminderMinutesBefore.HasValue && reminderMinutesBefore.Value < 5)
            return (false, "وقت التذكير يجب أن يكون 5 دقائق على الأقل");

        return (true, null);
    }

    /// <summary>
    /// التحقق من صحة جدول الحصص - Validate session schedule
    /// </summary>
    public static List<string> ValidateLiveSessionSchedule(string title, decimal price, DateTime startDate, DateTime endDate, int sessionCount)
    {
        var errors = new List<string>();

        if (string.IsNullOrWhiteSpace(title))
            errors.Add("عنوان جدول الحصص مطلوب");

        if (title?.Length > 300)
            errors.Add("عنوان جدول الحصص يجب ألا يتجاوز 300 حرف");

        if (price < 0)
            errors.Add("سعر الاشتراك يجب أن يكون 0 أو أكثر");

        if (endDate <= startDate)
            errors.Add("تاريخ النهاية يجب أن يكون بعد تاريخ البداية");

        if (startDate < DateTime.UtcNow.AddHours(-1))
            errors.Add("تاريخ البداية يجب أن يكون في المستقبل");

        if (sessionCount < 1)
            errors.Add("يجب إضافة جلسة واحدة على الأقل");

        if (sessionCount > 100)
            errors.Add("الحد الأقصى لعدد الجلسات هو 100 جلسة");

        return errors;
    }

    /// <summary>
    /// التحقق من تسعير الجلسة - Validate session pricing
    /// </summary>
    public static List<string> ValidateSessionPricing(decimal price, string pricingType)
    {
        var errors = new List<string>();

        if (pricingType == "Paid" && price <= 0)
            errors.Add("سعر الجلسة المدفوعة يجب أن يكون أكبر من صفر");

        if (price < 0)
            errors.Add("السعر لا يمكن أن يكون بالسالب");

        if (price > 100000)
            errors.Add("السعر يتجاوز الحد الأقصى المسموح");

        return errors;
    }

    /// <summary>
    /// التحقق من رفع التسجيل - Validate recording upload
    /// </summary>
    public static List<string> ValidateRecordingUpload(long fileSize, string? mimeType)
    {
        var errors = new List<string>();
        var maxSize = 2L * 1024 * 1024 * 1024; // 2GB
        var allowedTypes = new[] { "video/mp4", "video/webm", "video/mpeg", "video/quicktime", "video/x-msvideo" };

        if (fileSize <= 0)
            errors.Add("الملف فارغ");

        if (fileSize > maxSize)
            errors.Add("حجم الملف يتجاوز الحد الأقصى (2 جيجابايت)");

        if (!string.IsNullOrEmpty(mimeType) && !allowedTypes.Contains(mimeType.ToLower()))
            errors.Add("نوع الملف غير مدعوم. الأنواع المدعومة: MP4, WebM, MPEG, MOV, AVI");

        return errors;
    }

    /// <summary>
    /// التحقق من إمكانية النشر للوحدة - Check if module can be published
    /// </summary>
    public static (bool CanPublish, string? Reason) CanPublishModule(
        int lessonCount,
        bool hasAtLeastOneVideoLesson)
    {
        if (lessonCount == 0)
            return (false, "يجب إضافة دروس للوحدة قبل نشرها");

        if (!hasAtLeastOneVideoLesson && lessonCount < 2)
            return (false, "يجب إضافة درسين على الأقل أو درس فيديو واحد");

        return (true, null);
    }

    /// <summary>
    /// حساب صلاحية الأرباح للسحب - Calculate available earnings for withdrawal
    /// </summary>
    public static decimal CalculateAvailableEarnings(
        decimal totalEarnings,
        decimal pendingEarnings,
        decimal withdrawnAmount)
    {
        var available = totalEarnings - pendingEarnings - withdrawnAmount;
        return available > 0 ? available : 0;
    }

    /// <summary>
    /// التحقق من صلاحية إعدادات التقطير - Validate content drip settings
    /// </summary>
    public static (bool IsValid, string? Reason) ValidateContentDrip(
        ContentDripType dripType,
        int? daysAfterEnrollment,
        DateTime? specificDate)
    {
        if (dripType == ContentDripType.DaysAfterEnrollment)
        {
            if (!daysAfterEnrollment.HasValue || daysAfterEnrollment.Value < 0)
                return (false, "يجب تحديد عدد الأيام بعد التسجيل (صفر أو أكبر)");

            if (daysAfterEnrollment.Value > 365)
                return (false, "الحد الأقصى للأيام هو 365 يوم");
        }
        else if (dripType == ContentDripType.SpecificDate)
        {
            if (!specificDate.HasValue)
                return (false, "يجب تحديد التاريخ المحدد");

            if (specificDate.Value < DateTime.UtcNow)
                return (false, "التاريخ يجب أن يكون في المستقبل");
        }

        return (true, null);
    }

    /// <summary>
    /// التحقق من صلاحية معلومات الإعلان - Validate announcement settings
    /// </summary>
    public static (bool IsValid, string? Reason) ValidateAnnouncement(
        string title,
        string message,
        DateTime? scheduledFor)
    {
        if (string.IsNullOrWhiteSpace(title) || title.Length < 3)
            return (false, "العنوان يجب أن يكون 3 أحرف على الأقل");

        if (title.Length > 200)
            return (false, "العنوان يجب ألا يتجاوز 200 حرف");

        if (string.IsNullOrWhiteSpace(message) || message.Length < 10)
            return (false, "الرسالة يجب أن تكون 10 أحرف على الأقل");

        if (scheduledFor.HasValue && scheduledFor.Value < DateTime.UtcNow)
            return (false, "وقت الجدولة يجب أن يكون في المستقبل");

        return (true, null);
    }

    /// <summary>
    /// التحقق من صلاحية الرد على المراجعة - Validate review response
    /// </summary>
    public static (bool IsValid, string? Reason) ValidateReviewResponse(
        string response,
        bool alreadyResponded)
    {
        if (string.IsNullOrWhiteSpace(response))
            return (false, "الرد لا يمكن أن يكون فارغاً");

        if (response.Length < 10)
            return (false, "الرد يجب أن يكون 10 أحرف على الأقل");

        if (response.Length > 1000)
            return (false, "الرد يجب ألا يتجاوز 1000 حرف");

        return (true, null);
    }

    /// <summary>
    /// التحقق من صلاحية معلومات الدفع - Validate payment method details
    /// </summary>
    public static (bool IsValid, string? Reason) ValidatePaymentMethodDetails(
        WithdrawalMethodType payoutMethod,
        string? payPalEmail,
        string? bankName,
        string? bankAccountNumber,
        string? iban)
    {
        if (payoutMethod == WithdrawalMethodType.PayPal)
        {
            if (string.IsNullOrWhiteSpace(payPalEmail))
                return (false, "يجب إدخال بريد PayPal الإلكتروني");

            if (!payPalEmail.Contains("@") || !payPalEmail.Contains("."))
                return (false, "بريد PayPal غير صحيح");
        }
        else if (payoutMethod == WithdrawalMethodType.BankTransfer)
        {
            if (string.IsNullOrWhiteSpace(bankName))
                return (false, "يجب إدخال اسم البنك");

            if (string.IsNullOrWhiteSpace(bankAccountNumber))
                return (false, "يجب إدخال رقم الحساب البنكي");

            if (!string.IsNullOrWhiteSpace(iban))
            {
                // Basic IBAN validation (length check)
                var ibanClean = iban.Replace(" ", "").Replace("-", "");
                if (ibanClean.Length < 15 || ibanClean.Length > 34)
                    return (false, "رقم IBAN غير صحيح (يجب أن يكون بين 15 و 34 حرف)");
            }
        }

        return (true, null);
    }

    /// <summary>
    /// التحقق من صلاحية الباقة - Validate bundle settings
    /// </summary>
    public static (bool IsValid, string? Reason) ValidateBundle(
        int courseCount,
        decimal bundlePrice,
        decimal originalPrice,
        DateTime? validFrom,
        DateTime? validTo,
        int? maxSales)
    {
        if (courseCount < 2)
            return (false, "الباقة يجب أن تحتوي على دورتين على الأقل");

        if (bundlePrice <= 0)
            return (false, "سعر الباقة يجب أن يكون أكبر من صفر");

        if (bundlePrice >= originalPrice)
            return (false, "سعر الباقة يجب أن يكون أقل من مجموع أسعار الدورات");

        var discountPercentage = ((originalPrice - bundlePrice) / originalPrice) * 100;
        if (discountPercentage < 5)
            return (false, "يجب أن يكون الخصم 5% على الأقل");

        if (discountPercentage > 80)
            return (false, "الخصم لا يمكن أن يتجاوز 80%");

        if (validFrom.HasValue && validTo.HasValue)
        {
            if (validFrom.Value >= validTo.Value)
                return (false, "تاريخ الانتهاء يجب أن يكون بعد تاريخ البدء");

            if (validTo.Value < DateTime.UtcNow)
                return (false, "تاريخ الانتهاء يجب أن يكون في المستقبل");
        }

        if (maxSales.HasValue && maxSales.Value < 1)
            return (false, "الحد الأقصى للمبيعات يجب أن يكون 1 على الأقل");

        return (true, null);
    }

    /// <summary>
    /// التحقق من إمكانية حذف عنصر به مبيعات - Check if item with sales can be deleted
    /// </summary>
    public static (bool CanDelete, string? Reason) CanDeleteWithSales(int salesCount, bool isActive)
    {
        if (salesCount > 0)
            return (false, "لا يمكن حذف العنصر لأنه تم شراؤه من قبل. يمكنك تعطيله بدلاً من ذلك");

        if (isActive)
            return (false, "لا يمكن حذف عنصر نشط. قم بتعطيله أولاً");

        return (true, null);
    }

    /// <summary>
    /// التحقق من صلاحية محتوى المناقشة - Validate discussion content
    /// </summary>
    public static (bool IsValid, string? Reason) ValidateDiscussionContent(
        string content,
        bool isReply)
    {
        if (string.IsNullOrWhiteSpace(content))
            return (false, isReply ? "الرد لا يمكن أن يكون فارغاً" : "المناقشة لا يمكن أن تكون فارغة");

        var minLength = isReply ? 10 : 20;
        if (content.Length < minLength)
            return (false, $"المحتوى يجب أن يكون {minLength} حرف على الأقل");

        if (content.Length > 5000)
            return (false, "المحتوى يجب ألا يتجاوز 5000 حرف");

        return (true, null);
    }

    /// <summary>
    /// التحقق من صلاحية رفع الملف - Validate file upload
    /// </summary>
    public static (bool IsValid, string? Reason) ValidateFileUpload(
        long fileSizeBytes,
        string fileExtension,
        string[] allowedExtensions,
        long maxSizeMB)
    {
        var maxSizeBytes = maxSizeMB * 1024 * 1024;
        
        if (fileSizeBytes > maxSizeBytes)
            return (false, $"حجم الملف يجب ألا يتجاوز {maxSizeMB} ميجابايت");

        if (fileSizeBytes == 0)
            return (false, "الملف فارغ");

        if (!allowedExtensions.Contains(fileExtension.ToLower().TrimStart('.')))
            return (false, $"نوع الملف غير مسموح. الأنواع المسموحة: {string.Join(", ", allowedExtensions)}");

        return (true, null);
    }

    /// <summary>
    /// التحقق من اكتمال ملف المدرس - Validate instructor profile completion
    /// </summary>
    public static (bool IsComplete, string? Reason) ValidateInstructorProfileCompletion(
        string? bio,
        string? headline,
        bool hasPaymentMethod,
        string status)
    {
        if (status != "Approved")
            return (false, "يجب أن يكون حسابك معتمداً");

        if (string.IsNullOrWhiteSpace(bio) || bio.Length < 50)
            return (false, "يجب إضافة سيرة ذاتية (50 حرف على الأقل)");

        if (string.IsNullOrWhiteSpace(headline))
            return (false, "يجب إضافة عنوان تعريفي");

        if (!hasPaymentMethod)
            return (false, "يجب إضافة معلومات الدفع لاستلام الأرباح");

        return (true, null);
    }

    /// <summary>
    /// التحقق من صلاحية التعليق - Validate comment content
    /// </summary>
    public static (bool IsValid, string? Reason) ValidateComment(
        string content,
        int? parentCommentId = null)
    {
        if (string.IsNullOrWhiteSpace(content))
            return (false, "التعليق لا يمكن أن يكون فارغاً");

        if (content.Length < 2)
            return (false, "التعليق يجب أن يكون حرفين على الأقل");

        if (content.Length > 2000)
            return (false, "التعليق يجب ألا يتجاوز 2000 حرف");

        return (true, null);
    }

    /// <summary>
    /// التحقق من صلاحية الرسالة - Validate message content
    /// </summary>
    public static (bool IsValid, string? Reason) ValidateMessage(
        string subject,
        string body,
        int? maxAttachments = null,
        int attachmentCount = 0)
    {
        if (string.IsNullOrWhiteSpace(subject))
            return (false, "الموضوع لا يمكن أن يكون فارغاً");

        if (subject.Length < 3)
            return (false, "الموضوع يجب أن يكون 3 أحرف على الأقل");

        if (subject.Length > 200)
            return (false, "الموضوع يجب ألا يتجاوز 200 حرف");

        if (string.IsNullOrWhiteSpace(body))
            return (false, "محتوى الرسالة لا يمكن أن يكون فارغاً");

        if (body.Length < 10)
            return (false, "محتوى الرسالة يجب أن يكون 10 أحرف على الأقل");

        if (body.Length > 10000)
            return (false, "محتوى الرسالة يجب ألا يتجاوز 10000 حرف");

        if (maxAttachments.HasValue && attachmentCount > maxAttachments.Value)
            return (false, $"الحد الأقصى للمرفقات هو {maxAttachments.Value}");

        return (true, null);
    }

    /// <summary>
    /// التحقق من صلاحية حضور الحصة المباشرة - Validate live class attendance
    /// </summary>
    public static (bool IsValid, string? Reason) ValidateAttendance(
        DateTime classStartTime,
        DateTime classEndTime,
        DateTime? joinedAt,
        DateTime? leftAt)
    {
        if (joinedAt.HasValue && joinedAt.Value < classStartTime.AddMinutes(-15))
            return (false, "لا يمكن تسجيل الحضور قبل 15 دقيقة من بداية الحصة");

        if (leftAt.HasValue && joinedAt.HasValue && leftAt.Value < joinedAt.Value)
            return (false, "وقت المغادرة يجب أن يكون بعد وقت الانضمام");

        if (joinedAt.HasValue && joinedAt.Value > classEndTime.AddMinutes(30))
            return (false, "لا يمكن تسجيل الحضور بعد 30 دقيقة من انتهاء الحصة");

        return (true, null);
    }

    /// <summary>
    /// التحقق من صلاحية محاولة الاختبار - Validate quiz attempt
    /// </summary>
    public static (bool IsValid, string? Reason) ValidateQuizAttempt(
        int attemptNumber,
        int? maxAttempts,
        DateTime? availableFrom,
        DateTime? availableUntil,
        DateTime currentTime)
    {
        if (maxAttempts.HasValue && attemptNumber > maxAttempts.Value)
            return (false, $"لقد استنفدت العدد المسموح من المحاولات ({maxAttempts.Value})");

        if (availableFrom.HasValue && currentTime < availableFrom.Value)
            return (false, "الاختبار غير متاح بعد");

        if (availableUntil.HasValue && currentTime > availableUntil.Value)
            return (false, "انتهت مدة الاختبار");

        return (true, null);
    }

    /// <summary>
    /// التحقق من صلاحية سؤال الاختبار - Validate quiz question
    /// </summary>
    public static (bool IsValid, string? Reason) ValidateQuizQuestion(
        QuestionType questionType,
        string questionText,
        int optionsCount,
        int correctAnswersCount,
        decimal points)
    {
        if (string.IsNullOrWhiteSpace(questionText) || questionText.Length < 5)
            return (false, "نص السؤال يجب أن يكون 5 أحرف على الأقل");

        if (questionText.Length > 1000)
            return (false, "نص السؤال يجب ألا يتجاوز 1000 حرف");

        if (points <= 0 || points > 100)
            return (false, "نقاط السؤال يجب أن تكون بين 0 و 100");

        if (questionType == QuestionType.MultipleChoice || questionType == QuestionType.SingleChoice)
        {
            if (optionsCount < 2)
                return (false, "يجب إضافة خيارين على الأقل");

            if (correctAnswersCount == 0)
                return (false, "يجب تحديد إجابة صحيحة واحدة على الأقل");

            if (questionType == QuestionType.SingleChoice && correctAnswersCount > 1)
                return (false, "يجب تحديد إجابة صحيحة واحدة فقط للاختيار الواحد");
        }

        return (true, null);
    }

    /// <summary>
    /// التحقق من صلاحية الأسئلة المتكررة - Validate FAQ entry
    /// </summary>
    public static (bool IsValid, string? Reason) ValidateFaq(
        string question,
        string answer)
    {
        if (string.IsNullOrWhiteSpace(question) || question.Length < 10)
            return (false, "السؤال يجب أن يكون 10 أحرف على الأقل");

        if (question.Length > 500)
            return (false, "السؤال يجب ألا يتجاوز 500 حرف");

        if (string.IsNullOrWhiteSpace(answer) || answer.Length < 20)
            return (false, "الإجابة يجب أن تكون 20 حرف على الأقل");

        if (answer.Length > 5000)
            return (false, "الإجابة يجب ألا يتجاوز 5000 حرف");

        return (true, null);
    }

    /// <summary>
    /// التحقق من صلاحية العروض السريعة - Validate flash sale
    /// </summary>
    public static (bool IsValid, string? Reason) ValidateFlashSale(
        decimal originalPrice,
        decimal salePrice,
        DateTime startDate,
        DateTime endDate,
        int? maxQuantity)
    {
        if (salePrice <= 0)
            return (false, "سعر العرض يجب أن يكون أكبر من صفر");

        if (salePrice >= originalPrice)
            return (false, "سعر العرض يجب أن يكون أقل من السعر الأصلي");

        var discountPercentage = ((originalPrice - salePrice) / originalPrice) * 100;
        if (discountPercentage < 10)
            return (false, "يجب أن يكون الخصم 10% على الأقل");

        if (startDate >= endDate)
            return (false, "تاريخ الانتهاء يجب أن يكون بعد تاريخ البدء");

        var duration = (endDate - startDate).TotalHours;
        if (duration > 168) // 7 days
            return (false, "مدة العرض يجب ألا تتجاوز 7 أيام");

        if (maxQuantity.HasValue && maxQuantity.Value < 1)
            return (false, "الكمية يجب أن تكون 1 على الأقل");

        return (true, null);
    }

    /// <summary>
    /// التحقق من صلاحية مسار التعلم - Validate learning path
    /// </summary>
    public static (bool IsValid, string? Reason) ValidateLearningPath(
        string title,
        string description,
        int courseCount,
        int? estimatedDurationHours)
    {
        if (string.IsNullOrWhiteSpace(title) || title.Length < 5)
            return (false, "العنوان يجب أن يكون 5 أحرف على الأقل");

        if (title.Length > 200)
            return (false, "العنوان يجب ألا يتجاوز 200 حرف");

        if (string.IsNullOrWhiteSpace(description) || description.Length < 50)
            return (false, "الوصف يجب أن يكون 50 حرف على الأقل");

        if (courseCount < 2)
            return (false, "مسار التعلم يجب أن يحتوي على دورتين على الأقل");

        if (estimatedDurationHours.HasValue && (estimatedDurationHours.Value < 1 || estimatedDurationHours.Value > 1000))
            return (false, "المدة المقدرة يجب أن تكون بين 1 و 1000 ساعة");

        return (true, null);
    }

    #region Book Rules - قواعد الكتب

    /// <summary>
    /// الحد الأدنى لسعر الكتاب - Minimum book price
    /// </summary>
    public const decimal MinimumBookPrice = 0.01m;

    /// <summary>
    /// الحد الأقصى لسعر الكتاب - Maximum book price
    /// </summary>
    public const decimal MaximumBookPrice = 99999.99m;

    /// <summary>
    /// الحد الأدنى لوصف الكتاب - Minimum book description length
    /// </summary>
    public const int MinimumBookDescriptionLength = 50;

    /// <summary>
    /// الحد الأدنى للوصف المختصر للكتاب - Minimum book short description length
    /// </summary>
    public const int MinimumBookShortDescriptionLength = 20;

    /// <summary>
    /// الحد الأقصى لحجم ملف الكتاب بالميجابايت - Maximum book file size in MB
    /// </summary>
    public const long MaxBookFileSizeMB = 500;

    /// <summary>
    /// الحد الأقصى لحجم صورة الغلاف بالميجابايت - Maximum cover image size in MB
    /// </summary>
    public const long MaxBookCoverImageSizeMB = 10;

    /// <summary>
    /// الحد الافتراضي للتحميلات - Default max downloads
    /// </summary>
    public const int DefaultMaxBookDownloads = 3;

    /// <summary>
    /// فترة صلاحية رمز التحميل بالساعات - Download token expiry hours
    /// </summary>
    public const int BookDownloadTokenExpiryHours = 24;

    /// <summary>
    /// التحقق من سعر الكتاب - Validate book price
    /// </summary>
    public static (bool IsValid, string? Reason) ValidateBookPrice(
        decimal price,
        decimal? discountPrice)
    {
        if (price < MinimumBookPrice)
            return (false, $"السعر يجب أن يكون {MinimumBookPrice} على الأقل");

        if (price > MaximumBookPrice)
            return (false, $"السعر يجب ألا يتجاوز {MaximumBookPrice}");

        if (discountPrice.HasValue)
        {
            if (discountPrice.Value >= price)
                return (false, "سعر الخصم يجب أن يكون أقل من السعر الأصلي");

            if (discountPrice.Value < 0)
                return (false, "سعر الخصم يجب أن يكون صفر أو أكبر");
        }

        return (true, null);
    }

    /// <summary>
    /// التحقق من إمكانية حذف الكتاب - Check if book can be deleted
    /// </summary>
    public static (bool CanDelete, string? Reason) CanDeleteBook(int salesCount, BookStatus status)
    {
        if (salesCount > 0)
            return (false, "لا يمكن حذف الكتاب لأنه تم شراؤه من قبل");

        if (status == BookStatus.Published)
            return (false, "لا يمكن حذف كتاب منشور. قم بإلغاء نشره أولاً");

        return (true, null);
    }

    /// <summary>
    /// التحقق من إمكانية نشر الكتاب - Check if book can be published
    /// </summary>
    public static (bool CanPublish, string? Reason) CanPublishBook(
        bool hasCoverImage,
        bool hasBookFile,
        bool hasPrice,
        bool isFree,
        int descriptionLength)
    {
        if (!hasCoverImage)
            return (false, "يجب إضافة صورة غلاف للكتاب");

        if (!hasBookFile)
            return (false, "يجب رفع ملف الكتاب");

        if (!isFree && !hasPrice)
            return (false, "يجب تحديد سعر للكتاب أو جعله مجاني");

        if (descriptionLength < MinimumBookDescriptionLength)
            return (false, $"الوصف يجب أن يكون {MinimumBookDescriptionLength} حرف على الأقل");

        return (true, null);
    }

    /// <summary>
    /// التحقق من إمكانية تحميل الكتاب - Check if book can be downloaded
    /// </summary>
    public static (bool CanDownload, string? Reason) CanDownloadBook(
        int currentDownloads,
        int maxDownloads,
        DateTime? expiresAt)
    {
        if (expiresAt.HasValue && expiresAt.Value < DateTime.UtcNow)
            return (false, "انتهت صلاحية الوصول للكتاب");

        if (currentDownloads >= maxDownloads)
            return (false, $"لقد استنفدت عدد التحميلات المسموح ({maxDownloads})");

        return (true, null);
    }

    /// <summary>
    /// التحقق من صلاحية رفع ملف الكتاب - Validate book file upload
    /// </summary>
    public static (bool IsValid, string? Reason) ValidateBookFileUpload(
        long fileSizeBytes,
        string fileExtension,
        BookFormat format)
    {
        var maxSizeBytes = MaxBookFileSizeMB * 1024 * 1024;

        if (fileSizeBytes > maxSizeBytes)
            return (false, $"حجم الملف يجب ألا يتجاوز {MaxBookFileSizeMB} ميجابايت");

        if (fileSizeBytes == 0)
            return (false, "الملف فارغ");

        var allowedExtensions = format switch
        {
            BookFormat.PDF => new[] { "pdf" },
            BookFormat.EPUB => new[] { "epub" },
            BookFormat.MOBI => new[] { "mobi", "azw", "azw3" },
            BookFormat.AudioMP3 => new[] { "mp3", "m4a", "m4b" },
            _ => new[] { "pdf", "epub", "mobi" }
        };

        var ext = fileExtension.ToLower().TrimStart('.');
        if (!allowedExtensions.Contains(ext))
            return (false, $"نوع الملف غير مسموح. الأنواع المسموحة: {string.Join(", ", allowedExtensions)}");

        return (true, null);
    }

    /// <summary>
    /// التحقق من صلاحية باقة الكتب - Validate book bundle
    /// </summary>
    public static (bool IsValid, string? Reason) ValidateBookBundle(
        int bookCount,
        decimal bundlePrice,
        decimal originalPrice,
        DateTime? validFrom,
        DateTime? validTo)
    {
        if (bookCount < 2)
            return (false, "الباقة يجب أن تحتوي على كتابين على الأقل");

        if (bundlePrice <= 0)
            return (false, "سعر الباقة يجب أن يكون أكبر من صفر");

        if (bundlePrice >= originalPrice)
            return (false, "سعر الباقة يجب أن يكون أقل من مجموع أسعار الكتب");

        var discountPercentage = ((originalPrice - bundlePrice) / originalPrice) * 100;
        if (discountPercentage < 5)
            return (false, "يجب أن يكون الخصم 5% على الأقل");

        if (discountPercentage > 80)
            return (false, "الخصم لا يمكن أن يتجاوز 80%");

        if (validFrom.HasValue && validTo.HasValue)
        {
            if (validFrom.Value >= validTo.Value)
                return (false, "تاريخ الانتهاء يجب أن يكون بعد تاريخ البدء");

            if (validTo.Value < DateTime.UtcNow)
                return (false, "تاريخ الانتهاء يجب أن يكون في المستقبل");
        }

        return (true, null);
    }

    /// <summary>
    /// حساب عمولة بيع الكتاب - Calculate book sale commission
    /// </summary>
    public static (decimal PlatformCommission, decimal InstructorAmount) CalculateBookCommission(
        decimal saleAmount,
        decimal instructorCommissionRate)
    {
        var platformRate = 100 - instructorCommissionRate;
        var platformCommission = saleAmount * (platformRate / 100);
        var instructorAmount = saleAmount - platformCommission;

        return (Math.Round(platformCommission, 2), Math.Round(instructorAmount, 2));
    }

    /// <summary>
    /// التحقق من صلاحية مراجعة الكتاب - Validate book review
    /// </summary>
    public static (bool IsValid, string? Reason) ValidateBookReview(
        int rating,
        string? comment)
    {
        if (rating < 1 || rating > 5)
            return (false, "التقييم يجب أن يكون بين 1 و 5");

        if (!string.IsNullOrEmpty(comment) && comment.Length > 2000)
            return (false, "التعليق يجب ألا يتجاوز 2000 حرف");

        return (true, null);
    }

    #endregion

    #region Instructor Profile Validations

    /// <summary>
    /// الحد الأدنى لطول السيرة الذاتية - Minimum bio length
    /// </summary>
    public const int MinimumInstructorBioLength = 50;

    /// <summary>
    /// الحد الأقصى لطول السيرة الذاتية - Maximum bio length
    /// </summary>
    public const int MaximumInstructorBioLength = 5000;

    /// <summary>
    /// الحد الأدنى لطول العنوان المهني - Minimum headline length
    /// </summary>
    public const int MinimumHeadlineLength = 10;

    /// <summary>
    /// الحد الأقصى لطول العنوان المهني - Maximum headline length
    /// </summary>
    public const int MaximumHeadlineLength = 200;

    /// <summary>
    /// التحقق من صلاحية السيرة الذاتية للمدرس - Validate instructor bio
    /// </summary>
    public static (bool IsValid, string? Reason) ValidateInstructorBio(string? bio)
    {
        if (string.IsNullOrWhiteSpace(bio))
            return (true, null); // Bio is optional

        if (bio.Length < MinimumInstructorBioLength)
            return (false, $"السيرة الذاتية يجب أن تكون {MinimumInstructorBioLength} حرف على الأقل");

        if (bio.Length > MaximumInstructorBioLength)
            return (false, $"السيرة الذاتية يجب ألا تتجاوز {MaximumInstructorBioLength} حرف");

        return (true, null);
    }

    /// <summary>
    /// التحقق من صلاحية العنوان المهني - Validate headline
    /// </summary>
    public static (bool IsValid, string? Reason) ValidateHeadline(string? headline)
    {
        if (string.IsNullOrWhiteSpace(headline))
            return (true, null); // Headline is optional

        if (headline.Length < MinimumHeadlineLength)
            return (false, $"العنوان المهني يجب أن يكون {MinimumHeadlineLength} أحرف على الأقل");

        if (headline.Length > MaximumHeadlineLength)
            return (false, $"العنوان المهني يجب ألا يتجاوز {MaximumHeadlineLength} حرف");

        return (true, null);
    }

    /// <summary>
    /// التحقق من صلاحية رابط URL - Validate URL format (absolute http/https only).
    /// </summary>
    public static (bool IsValid, string? Reason) ValidateUrl(string? url, string fieldName = "الرابط")
    {
        return ValidateUrl(url, fieldName, allowRelativePath: false);
    }

    /// <summary>
    /// التحقق من صلاحية رابط URL أو مسار نسبي - Validate URL or relative path (e.g. from media library).
    /// When allowRelativePath is true, accepts paths starting with / (app-hosted media).
    /// </summary>
    public static (bool IsValid, string? Reason) ValidateUrl(string? url, string fieldName, bool allowRelativePath)
    {
        if (string.IsNullOrWhiteSpace(url))
            return (true, null); // URL is optional

        var trimmed = url.Trim();

        // Allow relative paths (e.g. /uploads/..., /Media/...) from media library
        if (allowRelativePath && trimmed.StartsWith("/", StringComparison.Ordinal))
        {
            if (trimmed.Contains("..", StringComparison.Ordinal))
                return (false, $"{fieldName} غير صحيح");
            if (trimmed.Length > 500)
                return (false, $"{fieldName} طويل جداً");
            return (true, null);
        }

        if (!Uri.TryCreate(trimmed, UriKind.Absolute, out var uriResult))
            return (false, $"{fieldName} غير صحيح");

        if (uriResult.Scheme != Uri.UriSchemeHttp && uriResult.Scheme != Uri.UriSchemeHttps)
            return (false, $"{fieldName} يجب أن يبدأ بـ http:// أو https://");

        return (true, null);
    }

    /// <summary>
    /// التحقق من صلاحية رقم الهاتف المصري - Validate Egyptian phone number
    /// </summary>
    public static (bool IsValid, string? Reason) ValidateEgyptianPhoneNumber(string? phoneNumber)
    {
        if (string.IsNullOrWhiteSpace(phoneNumber))
            return (true, null); // Phone is optional

        // Remove spaces and dashes
        var cleaned = phoneNumber.Replace(" ", "").Replace("-", "");

        // Check for valid Egyptian mobile format
        // Should be 11 digits starting with 01 (or +201 for international format)
        if (cleaned.StartsWith("+2"))
            cleaned = cleaned.Substring(2);

        if (cleaned.Length != 11)
            return (false, "رقم الهاتف يجب أن يكون 11 رقم");

        if (!cleaned.StartsWith("01"))
            return (false, "رقم الهاتف يجب أن يبدأ بـ 01");

        var validPrefixes = new[] { "010", "011", "012", "015" };
        var prefix = cleaned.Substring(0, 3);
        if (!validPrefixes.Contains(prefix))
            return (false, "رقم الهاتف غير صحيح. يجب أن يبدأ بـ 010 أو 011 أو 012 أو 015");

        if (!cleaned.All(char.IsDigit))
            return (false, "رقم الهاتف يجب أن يحتوي على أرقام فقط");

        return (true, null);
    }

    /// <summary>
    /// تنظيف رقم المحفظة الإلكترونية - Clean mobile wallet number (removes all non-digit characters)
    /// </summary>
    public static string CleanMobileWalletNumber(string? number)
    {
        if (string.IsNullOrWhiteSpace(number))
            return string.Empty;

        // Remove all non-digit characters (spaces, dashes, plus signs, etc.)
        var cleaned = new string(number.Trim().Where(char.IsDigit).ToArray());

        // Handle international format (+2 prefix) - remove if present
        if (cleaned.StartsWith("2") && cleaned.Length == 12)
            cleaned = cleaned.Substring(1);

        return cleaned;
    }

    /// <summary>
    /// التحقق من صلاحية رقم المحفظة الإلكترونية - Validate mobile wallet number
    /// </summary>
    public static (bool IsValid, string? Reason) ValidateMobileWalletNumber(string? number, string provider)
    {
        if (string.IsNullOrWhiteSpace(number))
            return (false, "رقم المحفظة مطلوب");

        // Comprehensive cleaning: remove all non-digit characters
        var cleaned = CleanMobileWalletNumber(number);

        if (cleaned.Length != 11)
            return (false, "رقم المحفظة يجب أن يكون 11 رقم");

        if (!cleaned.All(char.IsDigit))
            return (false, "رقم المحفظة يجب أن يحتوي على أرقام فقط");

        // Validate that it starts with 01 (Egyptian mobile format)
        if (!cleaned.StartsWith("01"))
            return (false, "رقم المحفظة يجب أن يبدأ بـ 01");

        // Validate based on provider
        var providerLower = provider?.ToLower() ?? string.Empty;
        var validPrefixes = providerLower switch
        {
            "vodafonecash" => new[] { "010" },
            "orangemoney" => new[] { "012" },
            "etisalatcash" => new[] { "011" },
            "instapay" => new[] { "010", "011", "012", "015" }, // InstaPay accepts all Egyptian mobile networks
            "wecash" => new[] { "015" },
            _ => new[] { "010", "011", "012", "015" } // Default: accept all valid Egyptian mobile prefixes
        };

        var prefix = cleaned.Substring(0, 3);
        if (!validPrefixes.Contains(prefix))
            return (false, $"رقم المحفظة غير صحيح لمزود {provider}. يجب أن يبدأ بـ {string.Join(" أو ", validPrefixes)}");

        return (true, null);
    }

    /// <summary>
    /// التحقق من صلاحية البريد الإلكتروني - Validate email format
    /// </summary>
    public static (bool IsValid, string? Reason) ValidateEmail(string? email, string fieldName = "البريد الإلكتروني")
    {
        if (string.IsNullOrWhiteSpace(email))
            return (false, $"{fieldName} مطلوب");

        // Basic email validation
        var parts = email.Split('@');
        if (parts.Length != 2)
            return (false, $"{fieldName} غير صحيح");

        if (string.IsNullOrWhiteSpace(parts[0]) || string.IsNullOrWhiteSpace(parts[1]))
            return (false, $"{fieldName} غير صحيح");

        if (!parts[1].Contains('.'))
            return (false, $"{fieldName} غير صحيح");

        // Check for common typos
        var commonDomains = new[] { "gmail.com", "yahoo.com", "hotmail.com", "outlook.com" };
        var domain = parts[1].ToLower();
        
        // Check for misspellings of common domains
        if (domain == "gamil.com" || domain == "gmal.com")
            return (false, "هل تقصد gmail.com؟");

        if (domain == "yaho.com" || domain == "yahooo.com")
            return (false, "هل تقصد yahoo.com؟");

        return (true, null);
    }

    /// <summary>
    /// التحقق من صلاحية اسم المستخدم - Validate username
    /// </summary>
    public static (bool IsValid, string? Reason) ValidateUsername(string? username)
    {
        if (string.IsNullOrWhiteSpace(username))
            return (false, "اسم المستخدم مطلوب");

        if (username.Length < 3)
            return (false, "اسم المستخدم يجب أن يكون 3 أحرف على الأقل");

        if (username.Length > 30)
            return (false, "اسم المستخدم يجب ألا يتجاوز 30 حرف");

        // Only allow alphanumeric characters, underscores, and dots
        var validChars = username.All(c => char.IsLetterOrDigit(c) || c == '_' || c == '.');
        if (!validChars)
            return (false, "اسم المستخدم يمكن أن يحتوي على حروف وأرقام ونقاط وشرطات سفلية فقط");

        // Cannot start or end with a dot or underscore
        if (username.StartsWith(".") || username.StartsWith("_") || 
            username.EndsWith(".") || username.EndsWith("_"))
            return (false, "اسم المستخدم لا يمكن أن يبدأ أو ينتهي بنقطة أو شرطة سفلية");

        // Reserved usernames
        var reserved = new[] { "admin", "administrator", "root", "system", "support", "help" };
        if (reserved.Contains(username.ToLower()))
            return (false, "اسم المستخدم محجوز");

        return (true, null);
    }

    /// <summary>
    /// التحقق من صلاحية كلمة المرور - Validate password strength
    /// </summary>
    public static (bool IsValid, string? Reason, int Strength) ValidatePasswordStrength(string? password)
    {
        if (string.IsNullOrWhiteSpace(password))
            return (false, "كلمة المرور مطلوبة", 0);

        if (password.Length < 8)
            return (false, "كلمة المرور يجب أن تكون 8 أحرف على الأقل", 1);

        var strength = 0;

        // Check for lowercase
        if (password.Any(char.IsLower))
            strength++;

        // Check for uppercase
        if (password.Any(char.IsUpper))
            strength++;

        // Check for digits
        if (password.Any(char.IsDigit))
            strength++;

        // Check for special characters
        if (password.Any(c => !char.IsLetterOrDigit(c)))
            strength++;

        // Check for length
        if (password.Length >= 12)
            strength++;

        if (strength < 3)
            return (false, "كلمة المرور ضعيفة. يجب أن تحتوي على أحرف كبيرة وصغيرة وأرقام", strength);

        return (true, null, strength);
    }

    /// <summary>
    /// التحقق من صلاحية المؤهل - Validate qualification
    /// </summary>
    public static (bool IsValid, string? Reason) ValidateQualification(
        string? title,
        string? institution,
        int? year,
        string? certificateUrl)
    {
        if (string.IsNullOrWhiteSpace(title))
            return (false, "عنوان المؤهل مطلوب");

        if (title.Length > 200)
            return (false, "عنوان المؤهل يجب ألا يتجاوز 200 حرف");

        if (!string.IsNullOrWhiteSpace(institution) && institution.Length > 200)
            return (false, "اسم المؤسسة يجب ألا يتجاوز 200 حرف");

        if (year.HasValue)
        {
            if (year.Value < 1950)
                return (false, "سنة الحصول غير صحيحة");

            if (year.Value > DateTime.UtcNow.Year + 1)
                return (false, "سنة الحصول غير صحيحة");
        }

        if (!string.IsNullOrWhiteSpace(certificateUrl))
        {
            var urlValidation = ValidateUrl(certificateUrl, "رابط الشهادة");
            if (!urlValidation.IsValid)
                return urlValidation;
        }

        return (true, null);
    }

    /// <summary>
    /// حساب نسبة اكتمال الملف الشخصي - Calculate profile completeness
    /// </summary>
    public static int CalculateProfileCompleteness(
        bool hasName,
        bool hasProfilePicture,
        bool hasHeadline,
        bool hasBio,
        bool hasSpecializations,
        bool hasExperience,
        bool hasPaymentMethod,
        bool hasSocialLinks,
        bool hasIntroVideo,
        bool hasQualifications)
    {
        var score = 0;
        
        if (hasName) score += 10;
        if (hasProfilePicture) score += 15;
        if (hasHeadline) score += 10;
        if (hasBio) score += 15;
        if (hasSpecializations) score += 10;
        if (hasExperience) score += 5;
        if (hasPaymentMethod) score += 15;
        if (hasSocialLinks) score += 10;
        if (hasIntroVideo) score += 5;
        if (hasQualifications) score += 5;

        return score;
    }

    #endregion
}

