using System.Collections.Generic;
using UnityEngine;

namespace MalumMenu.Cheats;

// othermenu/Lobby/NocturneCloneFont.cs — 5x7 glyph font for text-from-clones builders.
internal static class CloneFont
{
    private const int W = 5;
    private const int H = 7;
    private const int CharGap = 2;
    private const int WordGap = 4;

    private static readonly Dictionary<char, string> Glyphs = new Dictionary<char, string>
    {
        [' '] = "     " + "     " + "     " + "     " + "     " + "     " + "     ",
        ['A'] = " ### " + "#   #" + "#   #" + "#####" + "#   #" + "#   #" + "#   #",
        ['B'] = "#### " + "#   #" + "#   #" + "#### " + "#   #" + "#   #" + "#### ",
        ['C'] = " ### " + "#   #" + "#    " + "#    " + "#    " + "#   #" + " ### ",
        ['D'] = "#### " + "#   #" + "#   #" + "#   #" + "#   #" + "#   #" + "#### ",
        ['E'] = "#####" + "#    " + "#    " + "#### " + "#    " + "#    " + "#####",
        ['F'] = "#####" + "#    " + "#    " + "#### " + "#    " + "#    " + "#    ",
        ['G'] = " ### " + "#   #" + "#    " + "# ###" + "#   #" + "#   #" + " ####",
        ['H'] = "#   #" + "#   #" + "#   #" + "#####" + "#   #" + "#   #" + "#   #",
        ['I'] = "#####" + "  #  " + "  #  " + "  #  " + "  #  " + "  #  " + "#####",
        ['J'] = "#####" + "    #" + "    #" + "    #" + "#   #" + "#   #" + " ### ",
        ['K'] = "#   #" + "#  # " + "# #  " + "##   " + "# #  " + "#  # " + "#   #",
        ['L'] = "#    " + "#    " + "#    " + "#    " + "#    " + "#    " + "#####",
        ['M'] = "#   #" + "## ##" + "# # #" + "#   #" + "#   #" + "#   #" + "#   #",
        ['N'] = "#   #" + "##  #" + "# # #" + "#  ##" + "#   #" + "#   #" + "#   #",
        ['O'] = " ### " + "#   #" + "#   #" + "#   #" + "#   #" + "#   #" + " ### ",
        ['P'] = "#### " + "#   #" + "#   #" + "#### " + "#    " + "#    " + "#    ",
        ['Q'] = " ### " + "#   #" + "#   #" + "#   #" + "# # #" + "#  ##" + " ####",
        ['R'] = "#### " + "#   #" + "#   #" + "#### " + "# #  " + "#  # " + "#   #",
        ['S'] = " ####" + "#    " + "#    " + " ### " + "    #" + "    #" + "#### ",
        ['T'] = "#####" + "  #  " + "  #  " + "  #  " + "  #  " + "  #  " + "  #  ",
        ['U'] = "#   #" + "#   #" + "#   #" + "#   #" + "#   #" + "#   #" + " ### ",
        ['V'] = "#   #" + "#   #" + "#   #" + "#   #" + "#   #" + " # # " + "  #  ",
        ['W'] = "#   #" + "#   #" + "#   #" + "# # #" + "# # #" + "## ##" + "#   #",
        ['X'] = "#   #" + "#   #" + " # # " + "  #  " + " # # " + "#   #" + "#   #",
        ['Y'] = "#   #" + "#   #" + " # # " + "  #  " + "  #  " + "  #  " + "  #  ",
        ['Z'] = "#####" + "    #" + "   # " + "  #  " + " #   " + "#    " + "#####",
        ['0'] = " ### " + "#   #" + "#   #" + "#   #" + "#   #" + "#   #" + " ### ",
        ['1'] = "  #  " + " ##  " + "  #  " + "  #  " + "  #  " + "  #  " + "#####",
        ['2'] = " ### " + "#   #" + "    #" + "   # " + "  #  " + " #   " + "#####",
        ['3'] = " ### " + "    #" + "    #" + " ### " + "    #" + "    #" + " ### ",
        ['4'] = "   # " + "  ## " + " # # " + "#  # " + "#####" + "   # " + "   # ",
        ['5'] = "#####" + "#    " + "#    " + "#### " + "    #" + "#   #" + " ### ",
        ['6'] = " ### " + "#    " + "#    " + "#### " + "#   #" + "#   #" + " ### ",
        ['7'] = "#####" + "    #" + "   # " + "  #  " + "  #  " + "  #  " + "  #  ",
        ['8'] = " ### " + "#   #" + "#   #" + " ### " + "#   #" + "#   #" + " ### ",
        ['9'] = " ### " + "#   #" + "#   #" + " ####" + "    #" + "#   #" + " ### ",
        ['!'] = "  #  " + "  #  " + "  #  " + "  #  " + "  #  " + "     " + "  #  ",
        ['?'] = " ### " + "#   #" + "    #" + "   # " + "  #  " + "     " + "  #  ",
        ['.'] = "     " + "     " + "     " + "     " + "     " + "     " + "  #  ",
        ['-'] = "     " + "     " + "     " + "#####" + "     " + "     " + "     ",
        [':'] = "     " + "  #  " + "     " + "     " + "  #  " + "     " + "     ",
    };

    internal static float GetTextWidth(string text, float px)
    {
        float w = 0f;
        for (int i = 0; i < text.Length; i++)
        {
            w += W * px;
            if (i < text.Length - 1)
                w += (text[i] == ' ' ? WordGap : CharGap) * px;
        }
        return w;
    }

    internal static List<Vector3> GetPositions(string text, float px)
    {
        var pos = new List<Vector3>(text.Length * 12);
        float total = GetTextWidth(text, px);
        float startX = -total * 0.5f;
        float startY = H * px * 0.5f - px * 0.5f;
        float cx = startX;

        foreach (char raw in text)
        {
            char c = char.ToUpperInvariant(raw);
            if (!Glyphs.TryGetValue(c, out string g))
            {
                cx += (W + CharGap) * px;
                continue;
            }

            if (c != ' ')
            {
                for (int row = 0; row < H; row++)
                    for (int col = 0; col < W; col++)
                        if (g[row * W + col] == '#')
                            pos.Add(new Vector3(cx + col * px, startY - row * px, 0f));
            }

            cx += W * px + (c == ' ' ? WordGap : CharGap) * px;
        }

        return pos;
    }
}
