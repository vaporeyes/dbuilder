// ABOUTME: Easing and interpolation utilities ported from UDB Source/Core/Geometry/InterpolationTools.cs.
// ABOUTME: Color overloads operate on uint ARGB to remove the UDB PixelColor dependency.

/*
 * Copyright (c) 2007 Pascal vd Heiden, www.codeimp.com
 * This program is released under GNU General Public License
 */

using System;

namespace DBuilder.Geometry;

public static class InterpolationTools
{
    public enum Mode
    {
        LINEAR,
        EASE_IN_OUT_SINE,
        EASE_IN_SINE,
        EASE_OUT_SINE,
    }

    public static double Interpolate(double val1, double val2, double delta, Mode mode) => mode switch
    {
        Mode.LINEAR => Linear(val1, val2, delta),
        Mode.EASE_IN_SINE => EaseInSine(val1, val2, delta),
        Mode.EASE_OUT_SINE => EaseOutSine(val1, val2, delta),
        Mode.EASE_IN_OUT_SINE => EaseInOutSine(val1, val2, delta),
        _ => throw new NotImplementedException("InterpolationTools.Interpolate: \"" + mode + "\" mode is not supported!"),
    };

    // Based on Robert Penner's original easing equations (http://www.robertpenner.com/easing/)
    public static float Linear(float val1, float val2, float delta) => delta * val2 + (1.0f - delta) * val1;
    public static double Linear(double val1, double val2, double delta) => delta * val2 + (1.0f - delta) * val1;

    /// <summary>Sinusoidal easing in: accelerating from zero velocity.</summary>
    public static double EaseInSine(double val1, double val2, double delta)
    {
        double f_val1 = val1;
        double f_val2 = val2 - f_val1;
        return -f_val2 * Math.Cos(delta * Angle2D.PIHALF) + f_val2 + f_val1;
    }

    /// <summary>Sinusoidal easing out: decelerating to zero velocity.</summary>
    public static double EaseOutSine(double val1, double val2, double delta)
    {
        return (val2 - val1) * Math.Sin(delta * Angle2D.PIHALF) + val1;
    }

    /// <summary>Sinusoidal easing in/out: acceleration until halfway, then deceleration.</summary>
    public static double EaseInOutSine(double val1, double val2, double delta)
    {
        return -(val2 - val1) / 2 * (Math.Cos(Angle2D.PI * delta) - 1) + val1;
    }

    //mxd
    public static uint InterpolateColor(uint c1, uint c2, double delta)
    {
        double invdelta = 1.0f - delta;
        byte a1 = (byte)((c1 >> 24) & 0xff), r1 = (byte)((c1 >> 16) & 0xff), g1 = (byte)((c1 >> 8) & 0xff), b1 = (byte)(c1 & 0xff);
        byte a2 = (byte)((c2 >> 24) & 0xff), r2 = (byte)((c2 >> 16) & 0xff), g2 = (byte)((c2 >> 8) & 0xff), b2 = (byte)(c2 & 0xff);
        byte a = (byte)(a1 * invdelta + a2 * delta);
        byte r = (byte)(r1 * invdelta + r2 * delta);
        byte g = (byte)(g1 * invdelta + g2 * delta);
        byte b = (byte)(b1 * invdelta + b2 * delta);
        return ((uint)a << 24) | ((uint)r << 16) | ((uint)g << 8) | b;
    }

    //mxd
    public static uint InterpolateColor(uint c1, uint c2, double delta, Mode mode)
    {
        byte a1 = (byte)((c1 >> 24) & 0xff), r1 = (byte)((c1 >> 16) & 0xff), g1 = (byte)((c1 >> 8) & 0xff), b1 = (byte)(c1 & 0xff);
        byte a2 = (byte)((c2 >> 24) & 0xff), r2 = (byte)((c2 >> 16) & 0xff), g2 = (byte)((c2 >> 8) & 0xff), b2 = (byte)(c2 & 0xff);
        byte a = (byte)Math.Round(Interpolate(a1, a2, delta, mode));
        byte r = (byte)Math.Round(Interpolate(r1, r2, delta, mode));
        byte g = (byte)Math.Round(Interpolate(g1, g2, delta, mode));
        byte b = (byte)Math.Round(Interpolate(b1, b2, delta, mode));
        return ((uint)a << 24) | ((uint)r << 16) | ((uint)g << 8) | b;
    }
}
