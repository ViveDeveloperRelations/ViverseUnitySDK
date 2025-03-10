using System;
using UnityEngine;

namespace ViverseWebGLAPI
{
    /// <summary>
    /// Specifies the geographical scope of the leaderboard query.
    /// </summary>
    [Serializable]
    public enum LeaderboardRegion
    {
        /// <summary>
        /// Retrieve global leaderboard rankings across all regions.
        /// </summary>
        Global,

        /// <summary>
        /// Retrieve leaderboard rankings specific to the user's region.
        /// </summary>
        Local
    }

    /// <summary>
    /// Specifies the time period for the leaderboard query.
    /// </summary>
    [Serializable]
    public enum LeaderboardTimeRange
    {
        /// <summary>
        /// Retrieve rankings for the current day.
        /// </summary>
        Daily,

        /// <summary>
        /// Retrieve rankings for the current week.
        /// </summary>
        Weekly,

        /// <summary>
        /// Retrieve rankings for the current month.
        /// </summary>
        Monthly,

        /// <summary>
        /// Retrieve all-time rankings with no time restriction.
        /// </summary>
        Alltime
    }

    /// <summary>
    /// Configuration class for retrieving leaderboard data from the VIVERSE Game Dashboard.
    /// This class allows you to specify parameters for leaderboard queries including score ranges,
    /// time periods, and regional preferences.
    /// </summary>
    [Serializable]
    public class LeaderboardConfig
    {
        [SerializeField] private string name;

        [SerializeField] private int range_start = 0;

        [SerializeField] private int range_end = 100;

        [SerializeField] private string region = "global";

        [SerializeField] private string time_range = "alltime";

        [SerializeField] private bool around_user = false;

        /// <summary>
        /// Gets or sets the leaderboard's meta name. This is a unique identifier for the specific
        /// leaderboard you want to query within your application.
        /// </summary>
        /// <remarks>
        /// The meta name must be pre-configured in your VIVERSE application settings.
        /// </remarks>
        public string Name
        {
            get => name;
            set => name = value;
        }

        /// <summary>
        /// Gets or sets the starting rank position for the query range.
        /// </summary>
        /// <remarks>
        /// When <see cref="AroundUser"/> is false:
        /// - Must be greater than or equal to 0
        /// - Represents the absolute starting position in the leaderboard
        /// 
        /// When <see cref="AroundUser"/> is true:
        /// - Must be less than or equal to 0
        /// - Represents the number of positions above the current user's position to include
        /// </remarks>
        /// <exception cref="ArgumentException">
        /// Thrown when the value violates the constraints based on the <see cref="AroundUser"/> setting.
        /// </exception>
        public int RangeStart
        {
            get => range_start;
            set
            {
                if (around_user && value > 0)
                    throw new ArgumentException(
                        "When around_user is true, range_start must be less than or equal to 0");
                if (!around_user && value < 0)
                    throw new ArgumentException(
                        "When around_user is false, range_start must be greater than or equal to 0");
                range_start = value;
            }
        }

        /// <summary>
        /// Gets or sets the ending rank position for the query range.
        /// </summary>
        /// <remarks>
        /// When <see cref="AroundUser"/> is false:
        /// - Represents the absolute ending position in the leaderboard
        /// 
        /// When <see cref="AroundUser"/> is true:
        /// - Represents the number of positions below the current user's position to include
        /// - Must be greater than or equal to 0
        /// </remarks>
        /// <exception cref="ArgumentException">
        /// Thrown when the value is negative.
        /// </exception>
        public int RangeEnd
        {
            get => range_end;
            set
            {
                if (value < 0)
                    throw new ArgumentException("RangeEnd must be greater than or equal to 0");
                range_end = value;
            }
        }

        /// <summary>
        /// Gets or sets the geographical scope of the leaderboard query.
        /// </summary>
        /// <remarks>
        /// Global: Retrieve rankings from all regions
        /// Local: Retrieve rankings specific to the user's region
        /// </remarks>
        public LeaderboardRegion Region
        {
            get => region.ToLower() == "global" ? LeaderboardRegion.Global : LeaderboardRegion.Local;
            set => region = value.ToString().ToLower();
        }

        /// <summary>
        /// Gets or sets the time period for the leaderboard query.
        /// </summary>
        /// <remarks>
        /// Determines the time window for which scores are included in the rankings:
        /// - Daily: Current day's scores
        /// - Weekly: Current week's scores
        /// - Monthly: Current month's scores
        /// - Alltime: All scores regardless of when they were achieved
        /// </remarks>
        public LeaderboardTimeRange TimeRange
        {
            get => ParseTimeRange(time_range);
            set => time_range = value.ToString().ToLower();
        }

        /// <summary>
        /// Gets or sets whether to center the query range around the current user's position.
        /// </summary>
        /// <remarks>
        /// When true:
        /// - RangeStart represents positions above the user (must be ≤ 0)
        /// - RangeEnd represents positions below the user (must be ≥ 0)
        /// - The user's score will always be included in the results
        /// 
        /// When false:
        /// - RangeStart and RangeEnd represent absolute positions in the leaderboard
        /// - The user's score may or may not be included depending on their rank
        /// </remarks>
        public bool AroundUser
        {
            get => around_user;
            set
            {
                around_user = value;
                if (value && range_start > 0)
                    range_start = 0;
                if (!value && range_start < 0)
                    range_start = 0;
            }
        }

        private static LeaderboardTimeRange ParseTimeRange(string timeRange)
        {
            return timeRange.ToLower() switch
            {
                "daily" => LeaderboardTimeRange.Daily,
                "weekly" => LeaderboardTimeRange.Weekly,
                "monthly" => LeaderboardTimeRange.Monthly,
                "alltime" => LeaderboardTimeRange.Alltime,
                _ => throw new ArgumentException($"Invalid time range: {timeRange}")
            };
        }

        /// <summary>
        /// Creates a default leaderboard configuration with standard settings.
        /// </summary>
        /// <param name="leaderboardName">The unique identifier for the leaderboard to query.</param>
        /// <returns>A new LeaderboardConfig instance with default settings.</returns>
        /// <remarks>
        /// Default settings:
        /// - RangeStart: 0
        /// - RangeEnd: 100
        /// - Region: Global
        /// - TimeRange: Alltime
        /// - AroundUser: false
        /// </remarks>
        /// <exception cref="ArgumentException">
        /// Thrown when leaderboardName is null or empty.
        /// </exception>
        public static LeaderboardConfig CreateDefault(string leaderboardName)
        {
            if (string.IsNullOrEmpty(leaderboardName))
                throw new ArgumentException("Leaderboard name cannot be null or empty", nameof(leaderboardName));

            return new LeaderboardConfig
            {
                name = leaderboardName,
                range_start = 0,
                range_end = 100,
                region = "global",
                time_range = "alltime",
                around_user = false
            };
        }

        /// <summary>
        /// Creates a configuration for displaying scores centered around the current user's position.
        /// </summary>
        /// <param name="leaderboardName">The unique identifier for the leaderboard to query.</param>
        /// <param name="rangeAbove">Number of positions above the user's position to include.</param>
        /// <param name="rangeBelow">Number of positions below the user's position to include.</param>
        /// <returns>A new LeaderboardConfig instance configured to show scores around the user's position.</returns>
        /// <remarks>
        /// The resulting configuration will:
        /// - Set AroundUser to true
        /// - Include the specified number of positions above and below the user
        /// - Use global region and all-time rankings by default
        /// </remarks>
        /// <exception cref="ArgumentException">
        /// Thrown when leaderboardName is null or empty, or when either range parameter is negative.
        /// </exception>
        public static LeaderboardConfig CreateAroundUser(string leaderboardName, int rangeAbove, int rangeBelow)
        {
            if (rangeAbove < 0) throw new ArgumentException("Range above must be non-negative", nameof(rangeAbove));
            if (rangeBelow < 0) throw new ArgumentException("Range below must be non-negative", nameof(rangeBelow));

            return new LeaderboardConfig
            {
                name = leaderboardName,
                range_start = -rangeAbove,
                range_end = rangeBelow,
                region = "global",
                time_range = "alltime",
                around_user = true
            };
        }

        /// <summary>
        /// Validates the current configuration settings.
        /// </summary>
        /// <returns>true if the configuration is valid; otherwise, false.</returns>
        /// <remarks>
        /// Validates:
        /// - Name is not null or empty
        /// - Range values are appropriate for the AroundUser setting
        /// - Region and TimeRange values are valid
        /// </remarks>
        public bool Validate()
        {
            if (string.IsNullOrEmpty(name))
                return false;

            if (around_user)
            {
                if (range_start > 0)
                    return false;
            }
            else
            {
                if (range_start < 0)
                    return false;
            }

            if (range_end < 0)
                return false;

            try
            {
                _ = Region;
                _ = TimeRange;
            }
            catch
            {
                return false;
            }

            return true;
        }

        /// <summary>
        /// Returns a string representation of the leaderboard configuration.
        /// </summary>
        public override string ToString()
        {
            return $"LeaderboardConfig {{ Name: {name}, RangeStart: {range_start}, RangeEnd: {range_end}, " +
                   $"Region: {region}, TimeRange: {time_range}, AroundUser: {around_user} }}";
        }
    }
}