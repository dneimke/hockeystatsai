# Data Model

This document outlines the data model of the hockeystats database.

## Grade Names and their associated ShortNames

| Grade Name             | Short Name |
| ---------------------- | ---------- |
| Premier League Men     | PLM        |
| Premier League Women   | PLW        |
| Metro 1 Men            | M1M        |
| Metro 1 Women          | M1W        |
| Metro 2 Men            | M2M        |
| Metro 2 Women          | M2W        |
| Metro 3 Men            | M3M        |
| Metro 3 Women          | M3W        |
| Metro 4 Men            | M4M        |
| Metro 4 Women          | M4W        |
| Metro 5 Men            | M5M        |
| Metro 5 Women          | M5W        |
| Metro 6 Men            | M6M        |
| Metro 6 Women          | M6W        |

## Competition

| Column Name          | Data Type       |
| -------------------- | --------------- |
| Id                   | int             |
| AssociationId        | int             |
| CreatedDateTime      | datetimeoffset  |
| LastModifiedDateTime | datetimeoffset  |
| Name                 | nvarchar        |
| ShortName            | nvarchar        |
| SortOrder            | int             |

## CompetitionSeason

| Column Name          | Data Type       |
| -------------------- | --------------- |
| Id                   | int             |
| CompetitionId        | int             |
| CreatedDateTime      | datetimeoffset  |
| LastModifiedDateTime | datetimeoffset  |
| Name                 | nvarchar        |
| Ref                  | nvarchar        |
| IsCurrent            | bit             |
| Year                 | int             |
| CurrentRound         | int             |
| DefaultFixturesRound | int             |

## CompetitionTeam

| Column Name          | Data Type       |
| -------------------- | --------------- |
| Id                   | int             |
| ClubId               | int             |
| CompetitionSeasonId  | int             |
| CreatedDateTime      | datetimeoffset  |
| LastModifiedDateTime | datetimeoffset  |
| Name                 | nvarchar        |

## CompetitionFixture

| Column Name          | Data Type       |
| -------------------- | --------------- |
| Id                   | int             |
| AwayTeamId           | int             |
| CompetitionSeasonId  | int             |
| CreatedDateTime      | datetimeoffset  |
| HomeTeamId           | int             |
| LastModifiedDateTime | datetimeoffset  |
| Name                 | nvarchar        |
| LocationId           | int             |
| Time                 | datetime2       |
| Ref                  | nvarchar        |
| AwayTeamScore        | int             |
| HomeTeamScore        | int             |
| HasResult            | bit             |
| IsComplete           | bit             |
| HasAwayTeamStats     | bit             |
| HasHomeTeamStats     | bit             |
| RoundId              | int             |
| AwayStatsNotes       | nvarchar        |
| HasAwayStatsAnomalies| bit             |
| HasHomeStatsAnomalies| bit             |
| HomeStatsNotes       | nvarchar        |

## Club

| Column Name          | Data Type       |
| -------------------- | --------------- |
| Id                   | int             |
| AssociationId        | int             |
| CreatedDateTime      | datetimeoffset  |
| LastModifiedDateTime | datetimeoffset  |
| Name                 | nvarchar        |
| DisplayName          | nvarchar        |
| ShortName            | nvarchar        |

## Location

| Column Name          | Data Type       |
| -------------------- | --------------- |
| Id                   | int             |
| Address1             | nvarchar        |
| Address2             | nvarchar        |
| ClubId               | int             |
| CreatedDateTime      | datetimeoffset  |
| Description          | nvarchar        |
| LastModifiedDateTime | datetimeoffset  |
| Lat                  | int             |
| Lon                  | int             |
| Name                 | nvarchar        |
| Postcode             | int             |
| Suburb               | nvarchar        |

## Round

| Column Name          | Data Type       |
| -------------------- | --------------- |
| Id                   | int             |
| CompetitionSeasonId  | int             |
| CreatedDateTime      | datetimeoffset  |
| LastModifiedDateTime | datetimeoffset  |
| Name                 | nvarchar        |
| RoundNumber          | int             |
| Ref                  | nvarchar        |
| RoundTypeId          | int             |

## RoundType

| Column Name          | Data Type       |
| -------------------- | --------------- |
| Id                   | int             |
| CreatedDateTime      | datetimeoffset  |
| IsFinal              | bit             |
| SortOrder            | int             |
| LastModifiedDateTime | datetimeoffset  |
| Name                 | nvarchar        |
