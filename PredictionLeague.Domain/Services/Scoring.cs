namespace PredictionLeague.Domain.Services;

public static class Scoring
{
    public static int CalculatePoints(int actualHome, int actualAway, int predictedHome, int predictedAway)
    {
        const int correctScorePoints = 5;
        const int correctResultPoints = 3;
        const int incorrectPoints = 0;

        if (actualHome == predictedHome && actualAway == predictedAway)
            return correctScorePoints;

        var actualResult = Math.Sign(actualHome - actualAway);
        var predictedResult = Math.Sign(predictedHome - predictedAway);

        return actualResult == predictedResult ? correctResultPoints : incorrectPoints;
    }
}