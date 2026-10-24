@{
    # Order defines both the state string bindings and the layout mask bits.
    Basic = @(
        @{ Key = 'PlayTime'; Element = 'DunePlayTimeCard' }
        @{ Key = 'LastPlayed'; Element = 'DuneLastPlayedCard' }
        @{ Key = 'Completion'; Element = 'DuneCompletionCard' }
        @{ Key = 'Requirements'; Element = 'DuneRequirementsCard'; Clickable = $true }
    )
    Optional = @(
        @{ Key = 'HowLongToBeat'; Element = 'DuneHowLongToBeatCard'; Clickable = $true }
        @{ Key = 'Activity'; Element = 'DuneActivityCard'; Clickable = $true }
        @{ Key = 'Achievements'; Element = 'DuneAchievementsCard'; Clickable = $true }
        @{ Key = 'Languages'; Element = 'DuneLanguagesSummary'; Layout = 'Catalog' }
        @{ Key = 'Dlc'; Element = 'DuneDlcSummary'; Layout = 'Catalog' }
    )
}
