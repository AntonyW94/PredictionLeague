# Task 7: Testing Checklist

## Objective

Verify the implementation works correctly across all scenarios before considering the feature complete.

## Prerequisites

Before testing:

1. All previous tasks (1-6) must be completed
2. Solution must build without errors:
   ```bash
   dotnet build PredictionLeague.sln
   ```
3. API must be running:
   ```bash
   dotnet run --project PredictionLeague.API
   ```
4. Web client must be running:
   ```bash
   dotnet run --project PredictionLeague.Web
   ```

## Test Cases

### 1. Build Verification

- [ ] **1.1** Solution builds without errors
  ```bash
  dotnet build PredictionLeague.sln
  ```
- [ ] **1.2** No compiler warnings related to the new code

### 2. API Response Verification

- [ ] **2.1** Log in as a user who is a member of at least one league
- [ ] **2.2** Open browser DevTools (F12) → Network tab
- [ ] **2.3** Navigate to Dashboard
- [ ] **2.4** Find the `upcoming-rounds` API request
- [ ] **2.5** Verify response includes `matches` array for each round
- [ ] **2.6** Verify each match has:
  - `matchId` (number)
  - `homeTeamLogoUrl` (string or null)
  - `awayTeamLogoUrl` (string or null)
  - `predictedHomeScore` (number or null)
  - `predictedAwayScore` (number or null)

### 3. Desktop Layout (≥768px)

- [ ] **3.1** Open Dashboard on desktop browser (width ≥768px)
- [ ] **3.2** Verify "Upcoming Rounds" tile displays
- [ ] **3.3** Verify matches display in **2-column grid**
- [ ] **3.4** Verify each match row shows:
  - Home team logo (or placeholder)
  - Predicted home score (or "-")
  - "v" separator
  - Predicted away score (or "-")
  - Away team logo (or placeholder)
- [ ] **3.5** Verify logos are approximately 20px in size
- [ ] **3.6** Verify text is readable (white scores, grey "v")

### 4. Mobile Layout (<768px)

- [ ] **4.1** Resize browser below 768px OR use DevTools device toolbar
- [ ] **4.2** Verify matches display in **single column**
- [ ] **4.3** Verify all matches are visible without horizontal scrolling
- [ ] **4.4** Verify card doesn't overflow its container
- [ ] **4.5** Verify "Edit Predictions" button is still accessible below matches

### 5. Predictions Display

#### 5.1 User with predictions
- [ ] **5.1.1** Log in as a user who has submitted predictions for an upcoming round
- [ ] **5.1.2** Verify predicted scores display as numbers (e.g., "2 v 1")

#### 5.2 User without predictions
- [ ] **5.2.1** Log in as a user who has NOT submitted predictions
- [ ] **5.2.2** Verify scores display as dashes ("-" v "-")

#### 5.3 Partial predictions
- [ ] **5.3.1** If possible, have a user with predictions for some matches but not all
- [ ] **5.3.2** Verify predicted matches show scores, unpredicted show dashes

### 6. Team Logo Handling

#### 6.1 Valid logos
- [ ] **6.1.1** Verify team logos load and display correctly
- [ ] **6.1.2** Verify logos are not distorted (maintain aspect ratio)

#### 6.2 Placeholder (while loading)
- [ ] **6.2.1** On slow connection (DevTools → Network → Slow 3G), verify placeholder appears while logos load
- [ ] **6.2.2** Placeholder should be a simple grey circle/shield shape

#### 6.3 Broken/missing logos
- [ ] **6.3.1** Temporarily modify a logo URL in database to an invalid URL
- [ ] **6.3.2** Verify placeholder displays instead of broken image icon
- [ ] **6.3.3** Restore the correct URL after testing

### 7. Match Ordering

- [ ] **7.1** Verify matches are ordered by kickoff time (earliest first)
- [ ] **7.2** For matches with the same kickoff time, verify alphabetical order by home team short name
- [ ] **7.3** Compare order with the Edit Predictions page (should match)

### 8. Carousel Functionality

- [ ] **8.1** If user has multiple upcoming rounds, verify all rounds in carousel show matches
- [ ] **8.2** Navigate between rounds using carousel arrows/dots
- [ ] **8.3** Verify matches update correctly when switching rounds
- [ ] **8.4** Verify touch/swipe gestures still work on mobile

### 9. Edge Cases

#### 9.1 Round with no matches
- [ ] **9.1.1** If a round has no matches (edge case), verify no errors occur
- [ ] **9.1.2** Match grid section should not render (no empty container)

#### 9.2 Round with fewer than 10 matches
- [ ] **9.2.1** If a round has <10 matches, verify all display correctly
- [ ] **9.2.2** Layout should still look balanced

#### 9.3 Round with more than 10 matches
- [ ] **9.3.1** If a round has >10 matches, verify all display
- [ ] **9.3.2** Card height should accommodate all matches

#### 9.4 User not in any leagues
- [ ] **9.4.1** Log in as a user with no league memberships
- [ ] **9.4.2** Verify empty state still displays correctly (no upcoming rounds)

### 10. Performance

- [ ] **10.1** Verify page load time is acceptable (no noticeable slowdown)
- [ ] **10.2** Check Network tab - API response should be reasonably sized
- [ ] **10.3** Verify no console errors related to image loading

## Test Data Requirements

To properly test, you need:

1. **User with predictions**: A user who has submitted predictions for at least one upcoming round
2. **User without predictions**: A user who hasn't submitted predictions
3. **Multiple rounds**: At least 2 upcoming rounds to test carousel
4. **Typical round**: A round with ~10 matches

## Reporting Issues

If any test fails:

1. Note which test case failed (e.g., "5.2.2")
2. Describe the expected vs actual behaviour
3. Include browser console errors if any
4. Include screenshots if visual issue

## Sign-off

When all tests pass:

- [ ] All test cases verified
- [ ] No regressions in existing functionality
- [ ] Performance acceptable
- [ ] Code ready for deployment

## Rollback Plan

If issues are discovered post-deployment:

1. The changes are isolated to:
   - `UpcomingMatchDto.cs` (new file)
   - `UpcomingRoundDto.cs` (added property)
   - `GetUpcomingRoundsQueryHandler.cs` (SQL changes)
   - `RoundCard.razor` (markup changes)
   - `cards.css` (style additions)
   - `team-placeholder.svg` (new file)

2. To rollback:
   - Revert `UpcomingRoundDto.cs` to remove `Matches` property
   - Revert `GetUpcomingRoundsQueryHandler.cs` to previous SQL
   - Remove match grid markup from `RoundCard.razor`
   - Optionally leave CSS and SVG (harmless if unused)

3. No database changes required - this feature only reads data
