
using System;
using System.Collections.Generic;
using System.Linq;
using System.Speech.Synthesis;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;

namespace Bot
{
    public class QuizQuestion
    {
        public string Question { get; set; } = "";
        public List<string>? Options { get; set; }
        public string CorrectAnswer { get; set; } = "";
        public string Explanation { get; set; } = "";
    }

    public class TaskListItem
    {
        public int Id { get; set; }
        public string DisplayText { get; set; } = "";
        public string SubText { get; set; } = "";
        public override string ToString() =>
            string.IsNullOrEmpty(SubText) ? DisplayText : $"{DisplayText}\n   {SubText}";
    }

    internal static class QuizBank
    {
        internal static readonly List<QuizQuestion> Questions = new()
        {
            new QuizQuestion { Question = "What should you do if you receive an email asking for your password?", Options = new() { "A) Reply with your password", "B) Delete the email", "C) Report it as phishing", "D) Ignore it" }, CorrectAnswer = "C", Explanation = "Reporting phishing emails helps protect others and trains spam filters." },
            new QuizQuestion { Question = "True or False: Using the same password on multiple sites is safe as long as it is strong.", Options = new() { "A) True", "B) False" }, CorrectAnswer = "B", Explanation = "If one site is breached, attackers try that password everywhere (credential stuffing)." },
            new QuizQuestion { Question = "Which of the following is the most secure password?", Options = new() { "A) password123", "B) P@ssw0rd", "C) Tr0ub4dor&3!", "D) 12345678" }, CorrectAnswer = "C", Explanation = "A long passphrase with mixed characters is far harder to crack than common substitutions." },
            new QuizQuestion { Question = "True or False: Public Wi-Fi networks are safe for online banking.", Options = new() { "A) True", "B) False" }, CorrectAnswer = "B", Explanation = "Public Wi-Fi can be monitored. Always use a VPN or mobile data for sensitive transactions." },
            new QuizQuestion { Question = "What does 'HTTPS' in a URL indicate?", Options = new() { "A) The site is government-owned", "B) The connection is encrypted", "C) The site has been virus-scanned", "D) Downloads are safe" }, CorrectAnswer = "B", Explanation = "HTTPS means traffic between your browser and server is encrypted via TLS." },
            new QuizQuestion { Question = "Which social engineering technique uses phone calls to trick victims?", Options = new() { "A) Phishing", "B) Vishing", "C) Smishing", "D) Baiting" }, CorrectAnswer = "B", Explanation = "Vishing (voice phishing) uses phone calls. Smishing uses SMS; phishing uses email." },
            new QuizQuestion { Question = "True or False: Two-factor authentication (2FA) makes an account significantly harder to compromise.", Options = new() { "A) True", "B) False" }, CorrectAnswer = "A", Explanation = "2FA requires a second proof of identity, so a stolen password alone is not enough." },
            new QuizQuestion { Question = "What is 'ransomware'?", Options = new() { "A) Software that speeds up your PC", "B) Malware that encrypts your files and demands payment", "C) A type of firewall", "D) An antivirus program" }, CorrectAnswer = "B", Explanation = "Ransomware locks your data until you pay a ransom. Regular backups are the best defence." },
            new QuizQuestion { Question = "True or False: Clicking 'Unsubscribe' in a spam email is always safe.", Options = new() { "A) True", "B) False" }, CorrectAnswer = "B", Explanation = "Unsubscribe links in spam can confirm your address is active, inviting more spam or phishing." },
            new QuizQuestion { Question = "Which action best protects your personal data on social media?", Options = new() { "A) Using your real full name", "B) Making your profile public", "C) Reviewing and restricting privacy settings", "D) Sharing your location in every post" }, CorrectAnswer = "C", Explanation = "Restricting who sees your posts, location, and contact info limits data exposure." },
            new QuizQuestion { Question = "What is a 'zero-day' vulnerability?", Options = new() { "A) A bug patched on launch day", "B) An exploit with no available patch yet", "C) A type of DDoS attack", "D) Malware that deletes itself" }, CorrectAnswer = "B", Explanation = "Zero-day vulnerabilities are unknown to the vendor so no patch exists yet." },
            new QuizQuestion { Question = "True or False: Antivirus software alone is sufficient to protect against all cyber threats.", Options = new() { "A) True", "B) False" }, CorrectAnswer = "B", Explanation = "Good cybersecurity requires layered defences: AV, 2FA, backups, patching, and user awareness." },
        };
    }

    internal static class ActivityLog
    {
        private static readonly List<string> _entries = new();
        internal static void Add(string description) => _entries.Add($"[{DateTime.Now:HH:mm:ss}] {description}");
        internal static IReadOnlyList<string> GetRecent(int count = 10) => _entries.TakeLast(count).ToList().AsReadOnly();
        internal static IReadOnlyList<string> GetAll() => _entries.AsReadOnly();
    }

    public partial class MainWindow : Window
    {
        public delegate string TopicResponseDelegate();

        private string userName = "";
        private string userInterest = "";
        private string lastTopic = "";

        private bool _awaitingTaskTitle = false;
        private bool _awaitingTaskDescription = false;
        private bool _awaitingReminderAnswer = false;
        private bool _awaitingReminderDays = false;
        private string _pendingTaskTitle = "";
        private string _pendingTaskDescription = "";

        private bool _quizActive = false;
        private int _quizIndex = 0;
        private int _quizScore = 0;
        private bool _awaitingAnswer = false;
        private List<QuizQuestion> _shuffledQuestions = new();

        private readonly Dictionary<string, TopicResponseDelegate> topicDelegates;

        private readonly Dictionary<string, string> simpleResponses = new()
        {
            { "how are you", "I'm doing great! Ready to help you stay safe online." },
            { "what can you do", "I can teach you about passwords, phishing, privacy, and scams. Say 'add task', 'view tasks', 'start quiz', or 'show log'." },
            { "thank", "You're welcome! Stay vigilant." },
            { "bye", "Goodbye! Remember to use strong passwords." }
        };

        private readonly Dictionary<string, List<string>> randomTips = new()
        {
            { "password", new List<string> { "Use at least 12 characters with uppercase, lowercase, numbers, and symbols.", "Never reuse passwords across sites. Use a password manager!", "Avoid dictionary words or personal info like your birthday." }},
            { "phishing", new List<string> { "Always check the sender's email address before clicking links.", "Hover over links to see the real URL before clicking.", "Look for urgent language or spelling errors — common in phishing emails." }},
            { "privacy",  new List<string> { "Review app permissions regularly — they might access more than needed.", "Use a VPN on public Wi-Fi to encrypt your data.", "Check social media privacy settings — limit who sees your info." }},
            { "scam",     new List<string> { "Hang up on unsolicited calls asking for personal info.", "Never pay upfront for a prize — real winnings don't ask for fees.", "If an offer seems too good to be true, it probably is." }}
        };

        private readonly Dictionary<string, string> sentimentMap = new()
        {
            { "worried", "worried" }, { "scared", "worried" }, { "anxious", "worried" },
            { "curious", "curious" }, { "interested", "curious" },
            { "frustrated", "frustrated" }, { "confused", "frustrated" }
        };

        private readonly SpeechSynthesizer speech = new();
        private readonly Random rand = new();

        public MainWindow()
        {
            InitializeComponent();
            topicDelegates = new Dictionary<string, TopicResponseDelegate>
            {
                { "password",   () => GetRandomTip("password") },
                { "passcode",   () => GetRandomTip("password") },
                { "passphrase", () => GetRandomTip("password") },
                { "phishing",   () => GetRandomTip("phishing") },
                { "privacy",    () => GetRandomTip("privacy")  },
                { "private",    () => GetRandomTip("privacy")  },
                { "data",       () => GetRandomTip("privacy")  },
                { "scam",       () => GetRandomTip("scam")     },
                { "fraud",      () => GetRandomTip("scam")     }
            };
            Loaded += MainWindow_Loaded;
        }

        private async void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            try { DatabaseHelper.InitialiseDatabase(); ActivityLog.Add("Database initialised."); }
            catch (Exception ex) { AddBotMessage($"Warning: Database unavailable ({ex.Message}). Tasks will not persist."); }
            AddBotMessage("Hello! I'm your Cybersecurity Awareness Bot.");
            await VoiceGreetingAsync();
            AddBotMessage("What's your name?");
        }

        private void ProcessInput()
        {
            string input = InputBox.Text.Trim();
            if (string.IsNullOrEmpty(input)) return;
            AddUserMessage(input);
            InputBox.Clear();
            string response = GetBotResponse(input);
            if (!string.IsNullOrEmpty(response)) AddBotMessage(response);
        }

        private void Send_Click(object sender, RoutedEventArgs e) => ProcessInput();
        private void InputBox_KeyDown(object sender, KeyEventArgs e) { if (e.Key == Key.Enter) ProcessInput(); }

        private string GetBotResponse(string input)
        {
            string lower = input.ToLower().Trim();

            if (lower is "exit" or "quit") return "Stay safe online! Goodbye!";

            if (string.IsNullOrEmpty(userName) && !lower.Contains("my name is"))
            {
                userName = input.Trim();
                TxtName.Text = userName;
                ActivityLog.Add($"User identified as '{userName}'.");
                return $"Nice to meet you, {userName}! Ask me about passwords, phishing, privacy, or scams. Or say 'add task', 'start quiz', or 'show log'.";
            }

            if (_awaitingTaskTitle) return HandleTaskTitle(input);
            if (_awaitingTaskDescription) return HandleTaskDescription(input);
            if (_awaitingReminderAnswer) return HandleReminderAnswer(lower);
            if (_awaitingReminderDays) return HandleReminderDays(lower);
            if (_awaitingAnswer) return HandleQuizAnswer(lower);

            if (lower.Contains("my name is"))
            {
                string[] parts = input.Split(new[] { "my name is", "My name is" }, StringSplitOptions.None);
                if (parts.Length > 1) { userName = parts[1].Trim(); TxtName.Text = userName; ActivityLog.Add($"Name updated to '{userName}'."); return $"Thanks {userName}! Ask me about cybersecurity or say 'start quiz' / 'add task'."; }
            }

            if (NlpMatch(lower, "show log", "show activity", "what have you done", "activity log", "recent actions")) return ShowActivityLog();
            if (NlpMatch(lower, "add task", "new task", "create task", "remind me to", "add a reminder", "set a reminder", "can you remind", "i want to add", "add reminder")) return StartAddTask(input);
            if (NlpMatch(lower, "view tasks", "show tasks", "list tasks", "my tasks", "show my tasks", "check tasks")) return ShowTasks();
            if (NlpMatch(lower, "complete task", "mark task", "done task", "finish task", "completed task")) return HandleCompleteTask(lower);
            if (NlpMatch(lower, "delete task", "remove task", "cancel task")) return HandleDeleteTask(lower);
            if (NlpMatch(lower, "start quiz", "play quiz", "quiz me", "take quiz", "begin quiz", "cybersecurity quiz", "test me", "i want a quiz")) return StartQuiz();
            if (NlpMatch(lower, "stop quiz", "quit quiz", "end quiz", "cancel quiz")) return StopQuiz();

            if (NlpMatch(lower, "i'm interested in", "i am interested in", "i like", "my favorite", "my favourite"))
            {
                foreach (string key in new[] { "password", "phishing", "privacy", "scam" })
                    if (lower.Contains(key)) { userInterest = key; TxtInterest.Text = userInterest; ActivityLog.Add($"Interest set to '{userInterest}'."); return $"Great! I'll remember that you're interested in {userInterest}. " + GetRandomTip(userInterest); }
            }

            if (NlpMatch(lower, "another tip", "tell me more", "explain more", "more info", "give me more", "more tips", "more about"))
            {
                if (!string.IsNullOrEmpty(lastTopic) && topicDelegates.ContainsKey(lastTopic))
                    return $"Here's another tip about {lastTopic}:\n{topicDelegates[lastTopic]()}";
                return "Please ask me about a specific topic first (like passwords or phishing).";
            }

            string sentiment = DetectSentiment(lower);
            string prefix = GetEmpathyPrefix(sentiment);

            foreach (var kvp in topicDelegates)
                if (lower.Contains(kvp.Key)) { lastTopic = kvp.Key; ActivityLog.Add($"NLP: topic '{lastTopic}' detected."); return prefix + kvp.Value(); }

            foreach (var kvp in simpleResponses)
                if (lower.Contains(kvp.Key)) return prefix + kvp.Value;

            if (!string.IsNullOrEmpty(userInterest) && NlpMatch(lower, "help", "suggest", "advice", "tip", "tips"))
                return $"Since you're interested in {userInterest}, here's a tip: {GetRandomTip(userInterest)}";

            return "I didn't quite understand that. Try asking about passwords, phishing, privacy, or scams. Or say 'add task', 'view tasks', 'start quiz', or 'show log'.";
        }

        private static bool NlpMatch(string lower, params string[] keywords) => keywords.Any(k => lower.Contains(k));

       

        private string StartAddTask(string rawInput)
        {
            string title = ExtractTaskTitleFromInput(rawInput);
            if (!string.IsNullOrEmpty(title)) { _pendingTaskTitle = title; _awaitingTaskDescription = true; return $"I'll add the task: \"{_pendingTaskTitle}\". Please give a short description (or press Enter to skip):"; }
            _awaitingTaskTitle = true;
            return "What is the title of the cybersecurity task you'd like to add?";
        }

        private static string ExtractTaskTitleFromInput(string raw)
        {
            string[] patterns = { "remind me to ", "add task ", "add a task to ", "add a task ", "create task ", "new task ", "set a reminder to ", "can you remind me to " };
            string lower = raw.ToLower();
            foreach (string p in patterns)
            {
                int idx = lower.IndexOf(p, StringComparison.Ordinal);
                if (idx >= 0) { string ex = raw[(idx + p.Length)..].Trim(' ', '.', '!', '?'); if (ex.Length > 2) return char.ToUpper(ex[0]) + ex[1..]; }
            }
            return "";
        }

        private string HandleTaskTitle(string input) { _pendingTaskTitle = input.Trim(); _awaitingTaskTitle = false; _awaitingTaskDescription = true; return $"Got it! Task: \"{_pendingTaskTitle}\". Please give a short description (or press Enter to skip):"; }

        private string HandleTaskDescription(string input)
        {
            _pendingTaskDescription = string.IsNullOrWhiteSpace(input) ? $"Cybersecurity task: {_pendingTaskTitle}" : input.Trim();
            _awaitingTaskDescription = false; _awaitingReminderAnswer = true;
            return $"Task \"{_pendingTaskTitle}\" noted.\nWould you like a reminder? (yes / no)";
        }

        private string HandleReminderAnswer(string lower)
        {
            _awaitingReminderAnswer = false;
            if (NlpMatch(lower, "yes", "yeah", "sure", "please", "yep", "ok", "okay", "remind")) { _awaitingReminderDays = true; return "How many days from today? (Enter a number, e.g. 3)"; }
            return SaveTask(null);
        }

        private string HandleReminderDays(string lower)
        {
            _awaitingReminderDays = false;
            int days = 0;
            foreach (string token in lower.Split(' ', ',', '.')) if (int.TryParse(token, out int d)) { days = d; break; }
            return SaveTask(days > 0 ? DateTime.Now.AddDays(days) : (DateTime?)null, days);
        }

        private string SaveTask(DateTime? reminder, int days = 0)
        {
            string title = _pendingTaskTitle; string desc = _pendingTaskDescription;
            _pendingTaskTitle = ""; _pendingTaskDescription = "";
            string reminderMsg = reminder.HasValue ? $"Reminder set for {reminder.Value:dd MMM yyyy} ({days} day{(days == 1 ? "" : "s")} from now)." : "No reminder set.";
            try
            {
                int id = DatabaseHelper.AddTask(title, desc, reminder);
                ActivityLog.Add($"Task added (ID {id}): '{title}'. {reminderMsg}");
                return $"Task saved: \"{title}\". {reminderMsg}";
            }
            catch (Exception ex)
            {
                ActivityLog.Add($"Task '{title}' noted (DB unavailable: {ex.Message}).");
                return $"Task \"{title}\" noted! {reminderMsg} (Database not available — won't persist after restart.)";
            }
        }

        private string ShowTasks()
        {
            try
            {
                var tasks = DatabaseHelper.GetAllTasks();
                if (tasks.Count == 0) return "You have no tasks yet. Say 'add task' to create one!";
                var sb = new System.Text.StringBuilder("Your cybersecurity tasks:\n");
                foreach (var t in tasks)
                {
                    string status = t.IsCompleted ? "[Done]" : "[Pending]";
                    string rem = t.ReminderDate.HasValue ? $" | Reminder: {t.ReminderDate.Value:dd MMM yyyy}" : "";
                    sb.AppendLine($"  [{t.Id}] {status} {t.Title}{rem}");
                    if (!string.IsNullOrEmpty(t.Description)) sb.AppendLine($"       {t.Description}");
                }
                sb.Append("Say 'complete task [id]' or 'delete task [id]' to manage tasks.");
                ActivityLog.Add("User viewed task list.");
                return sb.ToString();
            }
            catch (Exception ex) { return $"Could not retrieve tasks: {ex.Message}"; }
        }

        private string HandleCompleteTask(string lower)
        {
            int id = ExtractId(lower);
            if (id <= 0) return "Please specify the task ID, e.g. 'complete task 2'.";
            try { DatabaseHelper.CompleteTask(id); ActivityLog.Add($"Task ID {id} marked complete."); return $"Task {id} marked as completed. Great work!"; }
            catch (Exception ex) { return $"Error: {ex.Message}"; }
        }

        private string HandleDeleteTask(string lower)
        {
            int id = ExtractId(lower);
            if (id <= 0) return "Please specify the task ID, e.g. 'delete task 3'.";
            try { DatabaseHelper.DeleteTask(id); ActivityLog.Add($"Task ID {id} deleted."); return $"Task {id} deleted."; }
            catch (Exception ex) { return $"Error: {ex.Message}"; }
        }

        private static int ExtractId(string lower) { foreach (string t in lower.Split(' ', '#')) if (int.TryParse(t, out int id)) return id; return -1; }

        

        private string StartQuiz()
        {
            if (_quizActive) return "A quiz is already in progress! Answer the current question or say 'stop quiz'.";
            _shuffledQuestions = QuizBank.Questions.OrderBy(_ => rand.Next()).Take(10).ToList();
            _quizIndex = 0; _quizScore = 0; _quizActive = true;
            ActivityLog.Add("Quiz started.");
            AddBotMessage("Cybersecurity Quiz started! Answer each question with A, B, C, or D. Good luck!\n");
            return AskQuizQuestion();
        }

        private string AskQuizQuestion()
        {
            if (_quizIndex >= _shuffledQuestions.Count) return FinishQuiz();
            var q = _shuffledQuestions[_quizIndex]; _awaitingAnswer = true;
            var sb = new System.Text.StringBuilder();
            sb.AppendLine($"Question {_quizIndex + 1}/{_shuffledQuestions.Count}:"); sb.AppendLine(q.Question);
            if (q.Options != null) foreach (string opt in q.Options) sb.AppendLine("   " + opt);
            return sb.ToString().TrimEnd();
        }

        private string HandleQuizAnswer(string lower)
        {
            if (!_quizActive) return "";
            var q = _shuffledQuestions[_quizIndex];
            string ans = lower.Trim().ToUpper();
            if (ans.StartsWith("TRUE")) ans = "A";
            if (ans.StartsWith("FALSE")) ans = "B";
            if (ans.Length > 1 && (ans[1] == ')' || ans[1] == '.')) ans = ans[0].ToString();
            bool correct = ans == q.CorrectAnswer.ToUpper();
            if (correct) _quizScore++;
            _awaitingAnswer = false; _quizIndex++;
            ActivityLog.Add($"Quiz Q{_quizIndex}: {(correct ? "correct" : "wrong")}.");
            AddBotMessage(correct ? $"Correct! {q.Explanation}" : $"Incorrect. The answer was {q.CorrectAnswer}. {q.Explanation}");
            return AskQuizQuestion();
        }

        private string FinishQuiz()
        {
            _quizActive = false; _awaitingAnswer = false;
            int total = _shuffledQuestions.Count; double pct = (double)_quizScore / total * 100;
            string grade = pct >= 90 ? "Excellent! You're a cybersecurity pro!" : pct >= 70 ? "Good job! Keep sharpening your skills." : pct >= 50 ? "Not bad, but there's room to improve." : "Don't worry — keep studying to stay safe online!";
            ActivityLog.Add($"Quiz completed: {_quizScore}/{total} ({pct:F0}%).");
            return $"Quiz complete! Your score: {_quizScore}/{total} ({pct:F0}%)\n{grade}";
        }

        private string StopQuiz() { if (!_quizActive) return "No quiz is currently running."; _quizActive = false; _awaitingAnswer = false; ActivityLog.Add("Quiz stopped."); return "Quiz stopped. Say 'start quiz' to try again!"; }

        

        private static string ShowActivityLog()
        {
            var recent = ActivityLog.GetRecent(10);
            if (recent.Count == 0) return "No activity recorded yet.";
            var sb = new System.Text.StringBuilder("Recent activity log:\n");
            int i = 1; foreach (string entry in recent) sb.AppendLine($"  {i++}. {entry}");
            sb.Append("(Showing last 10 entries.)"); return sb.ToString();
        }

        
        private string GetRandomTip(string topic) { if (!randomTips.ContainsKey(topic)) return GetBasicInfo(topic); var tips = randomTips[topic]; return tips[rand.Next(tips.Count)]; }

        private static string GetBasicInfo(string topic) => topic switch { "password" => "Use strong, unique passwords for every account. Consider a password manager.", "phishing" => "Phishing attacks trick you via fake emails. Always verify the sender.", "scam" => "Scammers create urgency. Never share personal info over the phone.", "privacy" => "Protect your privacy by limiting what you share online and using VPNs.", _ => "Cybersecurity is about protecting your data. Start with strong passwords!" };

        private string DetectSentiment(string lower) { foreach (var kvp in sentimentMap) if (lower.Contains(kvp.Key)) return kvp.Value; return "neutral"; }

        private static string GetEmpathyPrefix(string sentiment) => sentiment switch { "worried" => "It's completely understandable to feel worried. ", "curious" => "Great question! ", "frustrated" => "I know this can be confusing. Let me help: ", _ => "" };

       

        private void AddUserMessage(string msg) { ChatList.Items.Add($" You: {msg}"); ChatList.ScrollIntoView(ChatList.Items[^1]); }
        private void AddBotMessage(string msg) { ChatList.Items.Add($"Bot: {msg}"); ChatList.ScrollIntoView(ChatList.Items[^1]); }

        private async Task VoiceGreetingAsync()
        {
            try { await Task.Run(() => speech.SpeakAsync("Hello! Welcome to the Cybersecurity Awareness Bot.")); }
            catch { }
        }

        

        private void RefreshTasks_Click(object sender, RoutedEventArgs e)
        {
            TaskList.Items.Clear();
            try
            {
                var tasks = DatabaseHelper.GetAllTasks();
                if (tasks.Count == 0) { TaskList.Items.Add("No tasks yet. Say 'add task' in the Chat tab."); return; }
                foreach (var t in tasks)
                {
                    string rem = t.ReminderDate.HasValue ? $"  Reminder: {t.ReminderDate.Value:dd MMM yyyy}" : "";
                    TaskList.Items.Add(new TaskListItem { Id = t.Id, DisplayText = $"[{t.Id}] {(t.IsCompleted ? "[Done]" : "[Pending]")} {t.Title}{rem}", SubText = t.Description });
                }
            }
            catch (Exception ex) { TaskList.Items.Add($"Could not load tasks: {ex.Message}"); }
        }

        private void CompleteSelected_Click(object sender, RoutedEventArgs e)
        {
            if (TaskList.SelectedItem is TaskListItem item)
            { try { DatabaseHelper.CompleteTask(item.Id); ActivityLog.Add($"Task ID {item.Id} marked complete via Tasks tab."); RefreshTasks_Click(sender, e); } catch (Exception ex) { MessageBox.Show($"Error: {ex.Message}"); } }
            else MessageBox.Show("Please select a task first.");
        }

        private void DeleteSelected_Click(object sender, RoutedEventArgs e)
        {
            if (TaskList.SelectedItem is TaskListItem item)
            {
                if (MessageBox.Show($"Delete: {item.DisplayText}?", "Confirm", MessageBoxButton.YesNo, MessageBoxImage.Warning) == MessageBoxResult.Yes)
                { try { DatabaseHelper.DeleteTask(item.Id); ActivityLog.Add($"Task ID {item.Id} deleted via Tasks tab."); RefreshTasks_Click(sender, e); } catch (Exception ex) { MessageBox.Show($"Error: {ex.Message}"); } }
            }
            else MessageBox.Show("Please select a task first.");
        }

        private void RefreshLog_Click(object sender, RoutedEventArgs e)
        {
            LogList.Items.Clear();
            var all = ActivityLog.GetAll();
            if (all.Count == 0) { LogList.Items.Add("No activity recorded yet."); return; }
            int i = 1; foreach (string entry in all.Reverse()) { LogList.Items.Add($"{i++}. {entry}"); if (i > 50) break; }
        }
    }
}
