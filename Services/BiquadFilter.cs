using EQAPO_Configurator.Models;

namespace EQAPO_Configurator.Services;

/// <summary>
/// Biquad filter frequency response calculation.
/// Based on Robert Bristow-Johnson's Audio EQ Cookbook formulas.
/// </summary>
public static class BiquadFilter
{
    public static (double magnitudeReal, double magnitudeImag) Response(EqBand band, double frequency, double sampleRate = 44100)
    {
        double w0c = 2 * Math.PI * band.Frequency / sampleRate;
        double cosW0 = Math.Cos(w0c);
        double sinW0 = Math.Sin(w0c);
        double alpha = sinW0 / (2 * band.Q);

        double w0 = 2 * Math.PI * frequency / sampleRate;

        double a0, a1, a2, b0, b1, b2;

        switch (band.FilterType)
        {
            case EqFilterType.PK: // Peaking EQ
                double A = Math.Pow(10, band.Gain / 40);
                a0 = 1 + alpha / A;
                a1 = -2 * cosW0;
                a2 = 1 - alpha / A;
                b0 = 1 + alpha * A;
                b1 = -2 * cosW0;
                b2 = 1 - alpha * A;
                break;

            case EqFilterType.LSC: // Low Shelf
                double As = Math.Pow(10, band.Gain / 40);
                double sqrtA = Math.Sqrt(As);
                double alphaS = sinW0 / 2 * Math.Sqrt((As + 1 / As) * (1 / 0.7071 - 1) + 2);
                a0 = (As + 1) + (As - 1) * cosW0 + 2 * sqrtA * alphaS;
                a1 = -2 * ((As - 1) + (As + 1) * cosW0);
                a2 = (As + 1) + (As - 1) * cosW0 - 2 * sqrtA * alphaS;
                b0 = As * ((As + 1) - (As - 1) * cosW0 + 2 * sqrtA * alphaS);
                b1 = 2 * As * ((As - 1) - (As + 1) * cosW0);
                b2 = As * ((As + 1) - (As - 1) * cosW0 - 2 * sqrtA * alphaS);
                break;

            case EqFilterType.HSC: // High Shelf
                double AsH = Math.Pow(10, band.Gain / 40);
                double sqrtAH = Math.Sqrt(AsH);
                double alphaSH = sinW0 / 2 * Math.Sqrt((AsH + 1 / AsH) * (1 / 0.7071 - 1) + 2);
                a0 = (AsH + 1) - (AsH - 1) * cosW0 + 2 * sqrtAH * alphaSH;
                a1 = 2 * ((AsH - 1) - (AsH + 1) * cosW0);
                a2 = (AsH + 1) - (AsH - 1) * cosW0 - 2 * sqrtAH * alphaSH;
                b0 = AsH * ((AsH + 1) + (AsH - 1) * cosW0 + 2 * sqrtAH * alphaSH);
                b1 = -2 * AsH * ((AsH - 1) + (AsH + 1) * cosW0);
                b2 = AsH * ((AsH + 1) + (AsH - 1) * cosW0 - 2 * sqrtAH * alphaSH);
                break;

            default:
                a0 = 1; a1 = 0; a2 = 0;
                b0 = 1; b1 = 0; b2 = 0;
                break;
        }

        // Evaluate transfer function H(z) = (b0 + b1*z^-1 + b2*z^-2) / (a0 + a1*z^-1 + a2*z^-2)
        // At z = e^(j*w0), we get the frequency response
        double cos2 = Math.Cos(2 * w0);
        double sin2 = Math.Sin(2 * w0);

        // Numerator
        double numReal = b0 + b1 * cosW0 + b2 * cos2;
        double numImag = -(b1 * sinW0 + b2 * sin2);

        // Denominator
        double denReal = a0 + a1 * cosW0 + a2 * cos2;
        double denImag = -(a1 * sinW0 + a2 * sin2);

        // H(z) = num/den
        double denMag2 = denReal * denReal + denImag * denImag;
        double hReal = (numReal * denReal + numImag * denImag) / denMag2;
        double hImag = (numImag * denReal - numReal * denImag) / denMag2;

        return (hReal, hImag);
    }

    public static double MagnitudeDb(EqBand band, double frequency, double sampleRate = 44100)
    {
        var (real, imag) = Response(band, frequency, sampleRate);
        double mag = Math.Sqrt(real * real + imag * imag);
        return 20 * Math.Log10(Math.Max(mag, 1e-10));
    }

    /// <summary>
    /// Calculate combined frequency response of all bands at a given frequency.
    /// </summary>
    public static double CombinedMagnitudeDb(IEnumerable<EqBand> bands, double frequency, double sampleRate = 44100)
    {
        double totalReal = 1.0;
        double totalImag = 0.0;

        foreach (var band in bands.Where(b => b.Enabled && b.Gain != 0))
        {
            var (r, i) = Response(band, frequency, sampleRate);
            // Multiply complex numbers
            double newReal = totalReal * r - totalImag * i;
            double newImag = totalReal * i + totalImag * r;
            totalReal = newReal;
            totalImag = newImag;
        }

        double mag = Math.Sqrt(totalReal * totalReal + totalImag * totalImag);
        return 20 * Math.Log10(Math.Max(mag, 1e-10));
    }

    /// <summary>
    /// Generate frequency response points for visualization.
    /// Returns (frequency, magnitudeDb) pairs from 20 Hz to 20 kHz.
    /// </summary>
    public static List<(double Freq, double Db)> GenerateResponseCurve(
        IEnumerable<EqBand> bands, int numPoints = 200, double sampleRate = 44100)
    {
        var points = new List<(double, double)>(numPoints);
        double minFreq = 20;
        double maxFreq = 20000;
        double logMin = Math.Log10(minFreq);
        double logMax = Math.Log10(maxFreq);

        for (int i = 0; i < numPoints; i++)
        {
            double logFreq = logMin + (logMax - logMin) * i / (numPoints - 1);
            double freq = Math.Pow(10, logFreq);
            double db = CombinedMagnitudeDb(bands, freq, sampleRate);
            points.Add((freq, db));
        }

        return points;
    }
}
