using System.ComponentModel.DataAnnotations;
using LMS.Domain.Entities.Assessments;
using LMS.Domain.Enums;

namespace LMS.Areas.Student.ViewModels;

/// <summary>
/// نموذج إجابة الاختبار - Quiz Answer ViewModel
/// </summary>
public class QuizAnswerViewModel
{
    public int AttemptId { get; set; }
    public Dictionary<int, int> Answers { get; set; } = new();
}

/// <summary>
/// نموذج عرض محاولة الاختبار - Quiz Attempt Display ViewModel
/// </summary>
public class QuizAttemptDisplayViewModel
{
    public int AttemptId { get; set; }
    public int Id { get => AttemptId; set => AttemptId = value; }
    public int QuizId { get; set; }
    public string QuizTitle { get; set; } = string.Empty;
    public int TotalQuestions { get; set; }
    public decimal Score { get; set; }
    public decimal TotalScore { get => Score; set => Score = value; }
    public decimal PercentageScore { get; set; }
    public decimal ScorePercentage { get => PercentageScore; set => PercentageScore = value; }
    public bool IsPassed { get; set; }
    public int? TimeLimitMinutes { get; set; }
    public DateTime StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public int AttemptNumber { get; set; }
    public QuizDisplayInfo? Quiz { get; set; }
}

/// <summary>
/// معلومات عرض الاختبار - Quiz Display Info
/// </summary>
public class QuizDisplayInfo
{
    public int Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public int TotalQuestions { get; set; }
    public int PassingScore { get; set; }
    public bool AllowMultipleAttempts { get; set; }
    public QuizLessonInfo? Lesson { get; set; }
    public int TimeLimit { get; set; } = 30; // Time limit in minutes
    public int? TimeLimitMinutes { get => TimeLimit; set => TimeLimit = value ?? 30; }
}

/// <summary>
/// معلومات الدرس للاختبار - Quiz Lesson Info
/// </summary>
public class QuizLessonInfo
{
    public int Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public QuizModuleInfo? Module { get; set; }
}

/// <summary>
/// معلومات الوحدة للاختبار - Quiz Module Info
/// </summary>
public class QuizModuleInfo
{
    public int Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public QuizCourseInfo? Course { get; set; }
}

/// <summary>
/// معلومات الدورة للاختبار - Quiz Course Info
/// </summary>
public class QuizCourseInfo
{
    public int Id { get; set; }
    public string Title { get; set; } = string.Empty;
}

/// <summary>
/// نموذج تسليم التكليف - Submit Assignment ViewModel
/// </summary>
public class SubmitAssignmentViewModel
{
    [Required]
    public int AssignmentId { get; set; }

    [Required(ErrorMessage = "محتوى التسليم مطلوب")]
    [Display(Name = "محتوى التسليم")]
    public string Content { get; set; } = string.Empty;

    [MaxLength(1000)]
    [Display(Name = "رابط الملف")]
    public string? FileUrl { get; set; }
}

/// <summary>
/// نموذج عرض التكليف - Assignment Display ViewModel
/// </summary>
public class AssignmentDisplayViewModel
{
    public int Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string CourseName { get; set; } = string.Empty;
    public DateTime? DueDate { get; set; }
    public int MaxGrade { get; set; }
    public bool IsSubmitted { get; set; }
    public AssignmentStatus? SubmissionStatus { get; set; }
    public decimal? Grade { get; set; }
    public string? Feedback { get; set; }
    public bool IsLate { get; set; }
    public int DaysRemaining { get; set; }
}

/// <summary>
/// نموذج محاولة الاختبار - Quiz Attempt ViewModel (for taking quiz)
/// </summary>
public class QuizTakingViewModel
{
    /// <summary>
    /// معرف المحاولة - Attempt ID
    /// </summary>
    public int AttemptId { get; set; }

    /// <summary>
    /// معرف الاختبار - Quiz ID
    /// </summary>
    public int QuizId { get; set; }

    /// <summary>
    /// عنوان الاختبار - Quiz title
    /// </summary>
    public string QuizTitle { get; set; } = string.Empty;

    /// <summary>
    /// التعليمات - Instructions
    /// </summary>
    public string? Instructions { get; set; }

    /// <summary>
    /// الحد الزمني بالدقائق - Time limit in minutes
    /// </summary>
    public int? TimeLimitMinutes { get; set; }

    /// <summary>
    /// وقت البدء - Started at
    /// </summary>
    public DateTime StartedAt { get; set; }

    /// <summary>
    /// وقت الانتهاء المحسوب - Calculated end time
    /// </summary>
    public DateTime? EndTime { get; set; }

    /// <summary>
    /// رقم المحاولة - Attempt number
    /// </summary>
    public int AttemptNumber { get; set; }

    /// <summary>
    /// الأسئلة - Questions
    /// </summary>
    public List<QuizQuestionViewModel> Questions { get; set; } = new();

    /// <summary>
    /// السماح بالعودة للسابق - Allow back navigation
    /// </summary>
    public bool AllowBackNavigation { get; set; } = true;

    /// <summary>
    /// سؤال واحد بالصفحة - One question per page
    /// </summary>
    public bool OneQuestionPerPage { get; set; } = false;
}

/// <summary>
/// نموذج سؤال الاختبار - Quiz Question ViewModel
/// </summary>
public class QuizQuestionViewModel
{
    public int Id { get; set; }
    public string QuestionText { get; set; } = string.Empty;
    public string QuestionType { get; set; } = "MultipleChoice";
    public int Points { get; set; }
    public int OrderIndex { get; set; }
    public string? ImageUrl { get; set; }
    public List<QuizOptionViewModel> Options { get; set; } = new();
    public List<QuizOptionViewModel> Answers { get => Options; set => Options = value; }
}

/// <summary>
/// نموذج خيار السؤال - Quiz Option ViewModel
/// </summary>
public class QuizOptionViewModel
{
    public int Id { get; set; }
    public string Text { get; set; } = string.Empty;
    public string AnswerText { get => Text; set => Text = value; }
    public int OrderIndex { get; set; }
}

/// <summary>
/// نموذج الاختبار التكيفي - Adaptive Quiz ViewModel
/// </summary>
public class AdaptiveQuizViewModel
{
    public int QuizId { get; set; }
    public string QuizTitle { get; set; } = string.Empty;
    public int AttemptId { get; set; }
    public QuizQuestionViewModel? CurrentQuestion { get; set; }
    public int QuestionNumber { get; set; }
    public int TotalQuestions { get; set; }
    public string DifficultyLevel { get; set; } = "Medium";
    public string CurrentDifficultyLevel { get => DifficultyLevel; set => DifficultyLevel = value; }
    public string CurrentDifficulty { get => DifficultyLevel; set => DifficultyLevel = value; }
    public string? Instructions { get; set; }
    public List<QuizQuestionViewModel> Questions { get; set; } = new();
    public int QuestionsAnswered { get; set; }
    public decimal CurrentScore { get; set; }
    public int CorrectStreak { get; set; }
}

/// <summary>
/// نموذج تحليل المحاولة - Attempt Analysis ViewModel
/// </summary>
public class AttemptAnalysis
{
    public int AttemptId { get; set; }
    public string QuizTitle { get; set; } = string.Empty;
    public decimal Score { get; set; }
    public decimal PercentageScore { get; set; }
    public int CorrectAnswers { get; set; }
    public int TotalQuestions { get; set; }
    public TimeSpan TimeTaken { get; set; }
    public Dictionary<string, int> CategoryPerformance { get; set; } = new();
    public List<QuestionAnalysisItem> QuestionAnalyses { get; set; } = new();
    
    // Support for both entity and display view model
    public QuizAttempt? Attempt { get; set; }
    public QuizAttemptDisplayViewModel? AttemptDisplay { get; set; }
    
    public int TotalCorrect { get => CorrectAnswers; set => CorrectAnswers = value; }
    public int TotalIncorrect { get => TotalQuestions - CorrectAnswers; set { } }
    public List<QuestionAnalysisItem> QuestionAnalysis { get => QuestionAnalyses; set => QuestionAnalyses = value; }
    public List<string> StrengthAreas { get; set; } = new();
    public List<string> WeaknessAreas { get; set; } = new();
}

/// <summary>
/// عنصر تحليل السؤال - Question Analysis Item (matches controller output)
/// </summary>
public class QuestionAnalysisItem
{
    public Question? Question { get; set; }
    public bool IsCorrect { get; set; }
    public QuestionOption? SelectedOption { get; set; }
    public QuestionOption? CorrectOption { get; set; }
    public decimal PointsAwarded { get; set; }
    public int TimeTakenSeconds { get; set; }
}

/// <summary>
/// نموذج تحليل السؤال - Question Analysis
/// </summary>
public class QuestionAnalysis
{
    public int QuestionId { get; set; }
    public string QuestionText { get; set; } = string.Empty;
    public bool IsCorrect { get; set; }
    public string? YourAnswer { get; set; }
    public string? CorrectAnswer { get; set; }
    public string? Explanation { get; set; }
    public decimal PointsAwarded { get; set; }
    public string? SelectedOption { get; set; }
    public string? CorrectOption { get; set; }
    public QuestionInfo? Question { get; set; }
}

/// <summary>
/// معلومات السؤال - Question Info
/// </summary>
public class QuestionInfo
{
    public int Id { get; set; }
    public string QuestionText { get; set; } = string.Empty;
    public string QuestionType { get; set; } = "MultipleChoice";
    public int Points { get; set; }
    public string? Explanation { get; set; }
    public string? ImageUrl { get; set; }
    public string? DifficultyLevel { get; set; }
    public List<OptionInfo> Options { get; set; } = new();
}

/// <summary>
/// معلومات الخيار - Option Info
/// </summary>
public class OptionInfo
{
    public int Id { get; set; }
    public string Text { get; set; } = string.Empty;
    public string OptionText { get => Text; set => Text = value; }
    public bool IsCorrect { get; set; }
    public int OrderIndex { get; set; }
}

/// <summary>
/// نموذج مقارنة المحاولات - Attempts Comparison ViewModel
/// </summary>
public class AttemptsComparison
{
    public string QuizTitle { get; set; } = string.Empty;
    public List<AttemptSummary> Attempts { get; set; } = new();
    public Dictionary<string, List<decimal>> PerformanceTrend { get; set; } = new();
    
    // Additional properties for views
    public decimal ImprovementRate { get; set; }
    public int TotalAttempts { get => Attempts.Count; }
    public AttemptSummary? BestAttempt { get; set; }
    public decimal AverageScore { get; set; }
}

/// <summary>
/// نموذج ملخص المحاولة - Attempt Summary
/// </summary>
public class AttemptSummary
{
    public int AttemptId { get; set; }
    public int Id { get => AttemptId; set => AttemptId = value; }
    public int AttemptNumber { get; set; }
    public decimal Score { get; set; }
    public decimal TotalScore { get => Score; set => Score = value; }
    public decimal PercentageScore { get; set; }
    public decimal ScorePercentage { get => PercentageScore; set => PercentageScore = value; }
    public DateTime CompletedAt { get; set; }
    public DateTime? StartedAt { get; set; }
    public TimeSpan TimeTaken { get; set; }
    public bool IsPassed { get; set; }
    public int QuizId { get; set; }
}

/// <summary>
/// نموذج عنصر لوحة الصدارة - Leaderboard Entry ViewModel
/// </summary>
public class LeaderboardEntry
{
    public int Rank { get; set; }
    public string StudentId { get; set; } = string.Empty;
    public string StudentName { get; set; } = string.Empty;
    public decimal Score { get; set; }
    public decimal TotalScore { get => Score; set => Score = value; }
    public decimal PercentageScore { get; set; }
    public decimal AverageScore { get; set; }
    public DateTime CompletedAt { get; set; }
    public DateTime? CompletionTime { get => CompletedAt; set => CompletedAt = value ?? DateTime.MinValue; }
    public bool IsCurrentUser { get; set; }
    public int TotalQuizzes { get; set; }
    public decimal PassRate { get; set; }
    public string? ProfilePictureUrl { get; set; }
    public int AttemptsCount { get; set; }
}

/// <summary>
/// نموذج لوحة تحليلات الاختبار - Quiz Analytics Dashboard ViewModel
/// </summary>
public class QuizAnalyticsDashboard
{
    public int TotalQuizzesTaken { get; set; }
    public decimal AverageScore { get; set; }
    public int TotalTimespent { get; set; }
    public int TotalTimeSpentMinutes { get => TotalTimespent; set => TotalTimespent = value; }
    public List<QuizPerformanceSummary> RecentQuizzes { get; set; } = new();
    public List<QuizPerformanceSummary> RecentAttempts { get => RecentQuizzes; set => RecentQuizzes = value; }
    public Dictionary<string, decimal> SubjectPerformance { get; set; } = new();
    public List<SubjectQuizPerformance> QuizzesBySubject { get; set; } = new();
    
    // Additional properties for views
    public decimal ImprovementTrend { get; set; }
    public int PassedQuizzes { get; set; }
    public decimal BestPerformance { get; set; }
    public decimal WorstPerformance { get; set; }
}

/// <summary>
/// أداء الاختبارات حسب المادة - Subject Quiz Performance
/// </summary>
public class SubjectQuizPerformance
{
    public string SubjectName { get; set; } = string.Empty;
    public string CategoryName { get => SubjectName; set => SubjectName = value; }
    public int QuizzesTaken { get; set; }
    public decimal PassRate { get; set; }
    public decimal AverageScore { get; set; }
}

/// <summary>
/// نموذج ملخص أداء الاختبار - Quiz Performance Summary
/// </summary>
public class QuizPerformanceSummary
{
    public int QuizId { get; set; }
    public int Id { get => QuizId; set => QuizId = value; }
    public string QuizTitle { get; set; } = string.Empty;
    public decimal BestScore { get; set; }
    public decimal Score { get => BestScore; set => BestScore = value; }
    public decimal PercentageScore { get; set; }
    public int AttemptsCount { get; set; }
    public DateTime LastAttemptDate { get; set; }
    public DateTime Date { get => LastAttemptDate; set => LastAttemptDate = value; }
    public DateTime? CompletedAt { get => LastAttemptDate; set => LastAttemptDate = value ?? DateTime.MinValue; }
    public bool IsPassed { get; set; }
    public QuizDisplayInfo? Quiz { get; set; }
}

/// <summary>
/// نموذج وضع التدريب - Practice Mode ViewModel
/// </summary>
public class PracticeModeViewModel
{
    public int QuizId { get; set; }
    public string QuizTitle { get; set; } = string.Empty;
    public List<QuizQuestionViewModel> Questions { get; set; } = new();
    public bool ShowAnswersImmediately { get; set; } = true;
    public string? Instructions { get; set; }
    public QuizDisplayInfo? Quiz { get; set; }
    public QuizQuestionViewModel? CurrentQuestion { get; set; }
    public int CurrentQuestionIndex { get; set; }
    public int TotalQuestions { get => Questions.Count; }
    public PracticeStats? Stats { get; set; }
}

public class PracticeStats
{
    public int CorrectAnswers { get; set; }
    public int TotalAnswered { get; set; }
    public int IncorrectAnswers { get; set; }
    public decimal Accuracy { get; set; }
    public decimal AccuracyRate { get => Accuracy; set => Accuracy = value; }
    public decimal SpeedScore { get; set; }
}

/// <summary>
/// نموذج التحدي السريع - Quick Challenge ViewModel
/// </summary>
public class QuickChallengeViewModel
{
    public int ChallengeId { get; set; }
    public string Title { get; set; } = string.Empty;
    public int QuestionCount { get; set; }
    public int TimeLimitSeconds { get; set; }
    public List<QuizQuestionViewModel> Questions { get; set; } = new();
    public int CourseId { get; set; }
    public QuizDisplayInfo? Quiz { get; set; }
    public QuizQuestionViewModel? CurrentQuestion { get; set; }
    public int CurrentQuestionIndex { get; set; }
    public int TotalQuestions { get => Questions.Count; }
    public PracticeStats? Stats { get; set; }
}

/// <summary>
/// نموذج توصية المراجعة - Review Recommendation ViewModel
/// </summary>
public class ReviewRecommendation
{
    public int QuizId { get; set; }
    public string QuizTitle { get; set; } = string.Empty;
    public string Topic { get => QuizTitle; set => QuizTitle = value; }
    public string TopicName { get => QuizTitle; set => QuizTitle = value; }
    public string RecommendationReason { get; set; } = string.Empty;
    public string Description { get => RecommendationReason; set => RecommendationReason = value; }
    public string Priority { get; set; } = "Medium"; // High, Medium, Low
    public decimal LastScore { get; set; }
    public decimal CurrentScore { get => LastScore; set => LastScore = value; }
    public decimal ScorePercentage { get => LastScore; set => LastScore = value; }
    public decimal TargetScore { get; set; } = 80;
    public DateTime LastAttemptDate { get; set; }
    public int PriorityLevel { get; set; }
    public int EstimatedReviewTime { get; set; } = 30;
    public List<string> SuggestedActions { get; set; } = new();
    public int EnrollmentId { get; set; }
    public int QuestionsWrong { get; set; }
    public int TotalQuestions { get; set; }
    public string? LessonUrl { get; set; }
    public int? RelatedQuizId { get; set; }
}

/// <summary>
/// نموذج تحليل المحاولة - Attempt Analysis ViewModel
/// </summary>
public class AttemptAnalysisViewModel
{
    public int AttemptId { get; set; }
    public string QuizTitle { get; set; } = string.Empty;
    public decimal Score { get; set; }
    public decimal PercentageScore { get; set; }
    public int CorrectAnswers { get; set; }
    public int TotalCorrect { get => CorrectAnswers; set => CorrectAnswers = value; }
    public int TotalQuestions { get; set; }
    public int TotalIncorrect { get => TotalQuestions - CorrectAnswers; }
    public TimeSpan TimeTaken { get; set; }
    public TimeSpan TimeSpent { get => TimeTaken; set => TimeTaken = value; }
    public decimal Accuracy { get; set; }
    public Dictionary<string, int> CategoryPerformance { get; set; } = new();
    public List<CategoryPerformanceItem> ByCategory { get; set; } = new();
    public List<QuestionAnalysis> QuestionAnalyses { get; set; } = new();
    public List<QuestionAnalysis> QuestionAnalysis { get => QuestionAnalyses; set => QuestionAnalyses = value; }
    public QuizAttemptDisplayViewModel? Attempt { get; set; }
    public QuizDisplayInfo? Quiz { get; set; }
    public List<string> StrengthAreas { get; set; } = new();
    public List<StrengthWeaknessItem> Strengths { get; set; } = new();
    public List<string> WeaknessAreas { get; set; } = new();
    public List<StrengthWeaknessItem> Weaknesses { get; set; } = new();
    public List<string> Recommendations { get; set; } = new();
}

public class CategoryPerformanceItem
{
    public string CategoryName { get; set; } = string.Empty;
    public int Correct { get; set; }
    public int Total { get; set; }
    public decimal ScorePercentage { get; set; }
}

public class StrengthWeaknessItem
{
    public string Topic { get; set; } = string.Empty;
    public decimal Score { get; set; }
    public string Description { get; set; } = string.Empty;
}

/// <summary>
/// نموذج مقارنة المحاولات - Compare Attempts ViewModel
/// </summary>
public class CompareAttemptsViewModel
{
    public string QuizTitle { get; set; } = string.Empty;
    public List<AttemptSummary> Attempts { get; set; } = new();
    public Dictionary<string, List<decimal>> PerformanceTrend { get; set; } = new();
    public decimal ImprovementRate { get; set; }
    public int TotalAttempts { get => Attempts.Count; }
    public AttemptSummary? BestAttempt { get; set; }
    public decimal AverageScore { get; set; }
    public QuizDisplayInfo? Quiz { get; set; }
    public AttemptSummary? Attempt1 { get; set; }
    public AttemptSummary? Attempt2 { get; set; }
    public int Attempt1Correct { get; set; }
    public int Attempt2Correct { get; set; }
    public int TotalQuestions { get; set; }
    public TimeSpan Attempt1Time { get; set; }
    public TimeSpan Attempt2Time { get; set; }
    public List<CategoryComparison> CategoryComparison { get; set; } = new();
}

public class CategoryComparison
{
    public string CategoryName { get; set; } = string.Empty;
    public decimal Attempt1Score { get; set; }
    public decimal Attempt2Score { get; set; }
    public decimal Improvement { get; set; }
}

/// <summary>
/// نموذج لوحة المتصدرين - Quiz Leaderboard ViewModel
/// </summary>
public class QuizLeaderboardViewModel
{
    public int QuizId { get; set; }
    public string QuizTitle { get; set; } = string.Empty;
    public List<LeaderboardEntry> TopStudents { get; set; } = new();
    public List<LeaderboardEntry> Entries { get => TopStudents; set => TopStudents = value; }
    public LeaderboardEntry? CurrentUserEntry { get; set; }
    public int TotalParticipants { get; set; }
    public QuizDisplayInfo? Quiz { get; set; }
    public string Period { get; set; } = "AllTime"; // AllTime, ThisWeek, ThisMonth
    public List<LeaderboardEntry> TopThree { get; set; } = new();
}

/// <summary>
/// نموذج تحليلات الاختبارات الخاصة بي - My Quiz Analytics ViewModel
/// </summary>
public class MyQuizAnalyticsViewModel
{
    public int TotalQuizzesTaken { get; set; }
    public decimal AverageScore { get; set; }
    public int TotalTimeSpentMinutes { get; set; }
    public int TotalTimespent { get => TotalTimeSpentMinutes; set => TotalTimeSpentMinutes = value; }
    public List<QuizPerformanceSummary> RecentQuizzes { get; set; } = new();
    public List<QuizPerformanceSummary> RecentAttempts { get => RecentQuizzes; set => RecentQuizzes = value; }
    public Dictionary<string, decimal> SubjectPerformance { get; set; } = new();
    public List<SubjectQuizPerformance> ByCategory { get; set; } = new();
    public List<SubjectQuizPerformance> QuizzesBySubject { get; set; } = new();
    public decimal ImprovementTrend { get; set; }
    public int PassedQuizzes { get; set; }
    public int QuizzesPassed { get => PassedQuizzes; set => PassedQuizzes = value; }
    public decimal BestPerformance { get; set; }
    public decimal HighestScore { get => BestPerformance; set => BestPerformance = value; }
    public decimal WorstPerformance { get; set; }
    public List<ReviewRecommendation> Recommendations { get; set; } = new();
    public List<QuizPerformanceSummary> PerformanceHistory { get; set; } = new();
}

/// <summary>
/// نموذج توصيات المراجعة - Review Recommendations ViewModel
/// </summary>
public class ReviewRecommendationsViewModel
{
    public List<ReviewRecommendation> Recommendations { get; set; } = new();
    public List<ReviewRecommendation> UrgentRecommendations { get; set; } = new();
    public List<ReviewRecommendation> TopicsToReview { get => Recommendations; set => Recommendations = value; }
    public int TotalRecommendations { get => Recommendations.Count; }
    public int CompletedReviews { get; set; }
    public QuizDisplayInfo? Quiz { get; set; }
    public int AttemptId { get; set; }
    public int EstimatedReviewTime { get; set; }
}


