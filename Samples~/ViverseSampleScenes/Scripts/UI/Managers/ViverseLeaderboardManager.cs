using System;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.UIElements;
using ViverseUI.Infrastructure;
using ViverseWebGLAPI;

namespace ViverseUI.Managers
{
    /// <summary>
    /// Manages leaderboard service operations and UI
    /// </summary>
    public class ViverseLeaderboardManager : ViverseManagerBase
    {
        // UI Elements
        private TextField _appIdInput;
        private TextField _leaderboardNameInput;
        private TextField _scoreInput;
        private Button _uploadScoreButton;
        private Button _getLeaderboardButton;
        private TextField _leaderboardResult;
        
        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="context">Service context</param>
        /// <param name="root">Root UI element</param>
        public ViverseLeaderboardManager(IViverseServiceContext context, VisualElement root) 
            : base(context, root)
        {
        }
        
        /// <summary>
        /// Initialize UI elements
        /// </summary>
        protected override void InitializeUIElements()
        {
            _appIdInput = Root.Q<TextField>("app-id-input");
            _leaderboardNameInput = Root.Q<TextField>("leaderboard-name-input");
            _scoreInput = Root.Q<TextField>("score-input");
            _uploadScoreButton = Root.Q<Button>("upload-score-button");
            _getLeaderboardButton = Root.Q<Button>("get-leaderboard-button");
            _leaderboardResult = Root.Q<TextField>("leaderboard-result");
            
            if (_uploadScoreButton == null || _getLeaderboardButton == null)
            {
                Debug.LogError("Leaderboard UI elements not found. Check UXML structure.");
            }
        }
        
        /// <summary>
        /// Setup event handlers
        /// </summary>
        protected override void SetupEventHandlers()
        {
            if (_uploadScoreButton != null)
                _uploadScoreButton.clicked += async () => await UploadScore();
                
            if (_getLeaderboardButton != null)
                _getLeaderboardButton.clicked += async () => await GetLeaderboard();
        }
        
        /// <summary>
        /// Load initial state - populate with default values if needed
        /// </summary>
        protected override void LoadInitialState()
        {
            // Set default test values if fields are empty
            if (_appIdInput != null && string.IsNullOrEmpty(_appIdInput.value))
            {
                _appIdInput.value = "64aa6613-4e6c-4db4-b270-67744e953ce0"; // Default test app ID
            }
            
            if (_leaderboardNameInput != null && string.IsNullOrEmpty(_leaderboardNameInput.value))
            {
                _leaderboardNameInput.value = "test_leaderboard"; // Default test leaderboard
            }
        }
        
        /// <summary>
        /// Cleanup event handlers
        /// </summary>
        protected override void CleanupEventHandlers()
        {
            if (_uploadScoreButton != null)
                _uploadScoreButton.clicked -= async () => await UploadScore();
                
            if (_getLeaderboardButton != null)
                _getLeaderboardButton.clicked -= async () => await GetLeaderboard();
        }
        
        /// <summary>
        /// Upload score to leaderboard
        /// </summary>
        private async Task UploadScore()
        {
            if (!CheckInitialization()) return;
            
            // Validate inputs
            string appId = _appIdInput?.value?.Trim();
            string leaderboardName = _leaderboardNameInput?.value?.Trim();
            string scoreText = _scoreInput?.value?.Trim();
            
            if (string.IsNullOrEmpty(appId))
            {
                UIState.ShowError("Please enter an App ID");
                return;
            }
            
            if (string.IsNullOrEmpty(leaderboardName))
            {
                UIState.ShowError("Please enter a leaderboard name");
                return;
            }
            
            if (string.IsNullOrEmpty(scoreText))
            {
                UIState.ShowError("Please enter a score");
                return;
            }
            
            if (!float.TryParse(scoreText, out float score))
            {
                UIState.ShowError("Please enter a valid numeric score");
                return;
            }
            
            UIState.SetLoading(true, "Uploading score...");
            
            try
            {
                var uploadResult = await Context.Core.LeaderboardService.UploadScore(
                    appId, 
                    leaderboardName, 
                    score.ToString()
                );
                
                if (uploadResult.IsSuccess)
                {
                    string successMessage = $"Score uploaded successfully:\n" +
                                          $"App ID: {appId}\n" +
                                          $"Leaderboard: {leaderboardName}\n" +
                                          $"Score: {score}\n" +
                                          $"Response: {uploadResult.SafePayload}"; // ✅ Use safe property access
                    
                    if (_leaderboardResult != null)
                        _leaderboardResult.value = successMessage;
                        
                    UIState.ShowMessage($"Score {score} uploaded to {leaderboardName}");
                }
                else
                {
                    // ✅ Use safe logging extension for comprehensive error reporting
                    uploadResult.LogError("Upload Score");
                    
                    string errorMessage = $"Failed to upload score: {uploadResult.ErrorMessage}";
                    
                    if (_leaderboardResult != null)
                        _leaderboardResult.value = errorMessage;
                        
                    UIState.ShowError(errorMessage);
                }
            }
            catch (Exception e)
            {
                string errorMessage = $"Error uploading score: {e.Message}";
                Debug.LogError(errorMessage);
                
                if (_leaderboardResult != null)
                    _leaderboardResult.value = errorMessage;
                    
                UIState.ShowError(errorMessage);
            }
            finally
            {
                UIState.SetLoading(false);
            }
        }
        
        /// <summary>
        /// Get leaderboard data
        /// </summary>
        private async Task GetLeaderboard()
        {
            if (!CheckInitialization()) return;
            
            // Validate inputs
            string appId = _appIdInput?.value?.Trim();
            string leaderboardName = _leaderboardNameInput?.value?.Trim();
            
            if (string.IsNullOrEmpty(appId))
            {
                UIState.ShowError("Please enter an App ID");
                return;
            }
            
            if (string.IsNullOrEmpty(leaderboardName))
            {
                UIState.ShowError("Please enter a leaderboard name");
                return;
            }
            
            UIState.SetLoading(true, "Getting leaderboard...");
            
            try
            {
                // Create leaderboard configuration
                var config = new LeaderboardConfig
                {
                    Name = leaderboardName,
                    RangeStart = 1,
                    RangeEnd = 10,
                    Region = LeaderboardRegion.Global,
                    TimeRange = LeaderboardTimeRange.Alltime,
                    AroundUser = false
                };
                
                var leaderboardResult = await Context.Core.LeaderboardService.GetLeaderboardScores(appId, config);
                
                if (leaderboardResult.IsSuccess && leaderboardResult.Data != null)
                {
                    var data = leaderboardResult.Data;
                    
                    string resultText = FormatLeaderboardResults(data);
                    
                    if (_leaderboardResult != null)
                        _leaderboardResult.value = resultText;
                        
                    UIState.ShowMessage($"Leaderboard retrieved: {data.ranking?.Length ?? 0} entries");
                }
                else
                {
                    string errorMessage = $"Failed to get leaderboard: {leaderboardResult.ErrorMessage}";
                    
                    if (_leaderboardResult != null)
                        _leaderboardResult.value = errorMessage;
                        
                    UIState.ShowError(errorMessage);
                }
            }
            catch (Exception e)
            {
                string errorMessage = $"Error getting leaderboard: {e.Message}";
                Debug.LogError(errorMessage);
                
                if (_leaderboardResult != null)
                    _leaderboardResult.value = errorMessage;
                    
                UIState.ShowError(errorMessage);
            }
            finally
            {
                UIState.SetLoading(false);
            }
        }
        
        /// <summary>
        /// Format leaderboard results for display
        /// </summary>
        /// <param name="leaderboard">Leaderboard data</param>
        /// <returns>Formatted string</returns>
        private string FormatLeaderboardResults(LeaderboardResult leaderboard)
        {
            if (leaderboard == null)
                return "No leaderboard data available";
                
            var result = $"Leaderboard Results:\n";
            result += $"Total Count: {leaderboard.total_count}\n";
            
            if (leaderboard.meta != null)
            {
                result += $"Leaderboard: {leaderboard.meta.meta_name}\n";
                result += $"App ID: {leaderboard.meta.app_id}\n";
                result += $"Sort Type: {leaderboard.meta.sort_type}\n";
                result += $"Data Type: {leaderboard.meta.data_type}\n";
            }
            
            result += "\nRankings:\n";
            result += "Rank | Name | Score\n";
            result += "-----+------+-------\n";
            
            if (leaderboard.ranking != null && leaderboard.ranking.Length > 0)
            {
                foreach (var entry in leaderboard.ranking)
                {
                    result += $"{entry.rank,4} | {entry.name,-20} | {entry.value}\n";
                }
            }
            else
            {
                result += "No entries found\n";
            }
            
            return result;
        }
        
        /// <summary>
        /// Set default values for testing
        /// </summary>
        /// <param name="appId">Default app ID</param>
        /// <param name="leaderboardName">Default leaderboard name</param>
        public void SetDefaultValues(string appId, string leaderboardName)
        {
            if (_appIdInput != null && !string.IsNullOrEmpty(appId))
                _appIdInput.value = appId;
                
            if (_leaderboardNameInput != null && !string.IsNullOrEmpty(leaderboardName))
                _leaderboardNameInput.value = leaderboardName;
        }
        
        /// <summary>
        /// Clear leaderboard results display
        /// </summary>
        public void ClearResults()
        {
            if (_leaderboardResult != null)
                _leaderboardResult.value = "";
        }
    }
}