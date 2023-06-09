﻿using System.Text;
using System.Text.RegularExpressions;

namespace DiaryBot.Core
{
    public static class Extention
    {
        public static string ToHtml(this string text)
        {
            // we assume there are no same tags inside each other(e.g. [\b]some [\b]bold[\b0] text[\b0]),
            // cause we are processing it with Insert() method
            // although it doesn't save from selftyped tags
            // if there are, it will leave them as plain text
            text = Regex.Replace(text, @"\[\\b\](.*?)\[\\b0\]", @$"<b>$1</b>");
            text = Regex.Replace(text, @"\[\\i\](.*?)\[\\i0\]", @$"<i>$1</i>");
            text = Regex.Replace(text, @"\[\\u\](.*?)\[\\u0\]", @$"<u>$1</u>");
            text = Regex.Replace(text, @"\[\\s\](.*?)\[\\s0\]", @$"<s>$1</s>");
            text = Regex.Replace(text, @"\[\\v\](.*?)\[\\v0\]", @$"<span class=""tg-spoiler"">$1</span>");
            return text;
        }

        public static string ToXaml(this string text)
        {
            // we assume there are no same tags inside each other(e.g. [\b]some [\b]bold[\b0] text[\b0]),
            // cause we are processing it with Insert() method
            // although it doesn't save from selftyped tags
            // if there are, it will leave them as plain text
            text = Regex.Replace(text, @"\[\\b\](.*?)\[\\b0\]", @$"<Span FontWeight=""Bold"">$1</Span>");
            text = Regex.Replace(text, @"\[\\i\](.*?)\[\\i0\]", @$"<Span FontStyle=""Italic"">$1</Span>");
            text = Regex.Replace(text, @"\[\\u\](.*?)\[\\u0\]", @$"<Span TextDecorations=""Underline"">$1</Span>");
            text = Regex.Replace(text, @"\[\\s\](.*?)\[\\s0\]", @$"<Span TextDecorations=""Strikethrough"">$1</Span>");
            text = Regex.Replace(text, @"\[\\v\](.*?)\[\\v0\]", @$"<Span Background=""LightGray"">$1</Span>");
            text = text.Replace("\r\n", "<LineBreak/>");
            return text;
        }

        public static string WrapWithTags(this string text, string tag, string textBefore, string textAfter)
        {
            // check if selectedText is already in needed tags
            bool hasTagOnLeft = false;
            bool hasTagOnRigth = false;

            for (int i = textBefore.Length - 1; i >= 4; i--)
            {
                if (textBefore[..i].EndsWith(tag[..4]))
                {
                    hasTagOnLeft = true;
                    break;
                }
                else if (textBefore[..i].EndsWith(tag[^5..]))
                    break;
            }

            for (int i = 0; i < textAfter.Length - 5; i++)
            {
                if (textAfter[i..].StartsWith(tag[^5..]))
                {
                    hasTagOnRigth = true;
                    break;
                }
                else if (textAfter[i..].StartsWith(tag[..4]))
                    break;
            }

            // if we detect only tag on one side, we will slide it to the start or the end of the selectedText
            // if there are detected tags from both sides, we return the original selectedText, there is no need to modify it
            if (hasTagOnLeft && hasTagOnRigth)
                return text;

            // tags are working bad with multilines, so we spliting selectedText and processing each line separately
            string[] tags = new[] { Tag.Bold, Tag.Italic, Tag.Underline, Tag.Strikethrough, Tag.Spoiler };
            string[] textLines = text.Split("\r\n");
            for (int i = 0; i < textLines.Length; i++)
            {
                if (!string.IsNullOrWhiteSpace(textLines[i]))
                {
                    // searching single tags that are not equal to input tag to fix possible error with laying tags on each other
                    // using priority queue to sort 
                    PriorityQueue<string, int> innerTagsPQ = new();
                    PriorityQueue<string, int> outerTagsPQ = new();
                    foreach (var item in tags)
                    {
                        if (item != tag)
                        {
                            int indexOfStartTag = textLines[i].IndexOf(item[..4]);
                            int indexOfEndTag = textLines[i].IndexOf(item[^5..]);
                            if (indexOfStartTag > -1 && indexOfEndTag == -1)
                            {
                                innerTagsPQ.Enqueue(item[^5..], -indexOfStartTag);
                                outerTagsPQ.Enqueue(item[..4], indexOfStartTag);
                            }
                            else if (indexOfStartTag == -1 && indexOfEndTag > -1)
                            {
                                innerTagsPQ.Enqueue(item[..4], -indexOfStartTag);
                                outerTagsPQ.Enqueue(item[^5..], indexOfStartTag);
                            }
                        }
                    }

                    StringBuilder innerPrefix = new(), innerPostfix = new();
                    StringBuilder outerPrefix = new(), outerPostfix = new();
                    while (innerTagsPQ.Count > 0 || outerTagsPQ.Count > 0)
                    {
                        if (innerTagsPQ.Count > 0)
                        {
                            string item = innerTagsPQ.Dequeue();
                            if (item.Length == 4) //start tag's (e.g.[\i]) length is 4, end's is 5
                            {
                                innerPrefix.Append(item);
                            }
                            else
                            {
                                innerPostfix.Append(item);
                            }
                        }

                        if (outerTagsPQ.Count > 0)
                        {
                            string item = outerTagsPQ.Dequeue();
                            if (item.Length == 5) //end tag's (e.g.[\i]) length is 5, end's is 5
                            {
                                outerPrefix.Append(item);
                            }
                            else
                            {
                                outerPostfix.Append(item);
                            }
                        }
                    }

                    // after we summarized all prefixes and postfixes, we add inner ones to current, not yet formatted textline
                    // outers are added after textline has been formatted
                    textLines[i] = innerPrefix.ToString() + textLines[i] + innerPostfix.ToString();

                    // if we detect both tags inside a selected line, we remove them as we are expanding a tag zone
                    if (textLines[i].Contains(tag[..4]) && textLines[i].Contains(tag[^5..]))
                        textLines[i] = outerPrefix.ToString() + tag.Replace("{message}", textLines[i].Replace(tag[..4], "").Replace(tag[^5..], "")) + outerPostfix.ToString();
                    // if we detect only a start tag inside a selected line, we remove it and add an end tag to the end of a selected line
                    else if (textLines[i].Contains(tag[..4]))
                        textLines[i] = outerPrefix.ToString() + tag[..^5].Replace("{message}", textLines[i].Replace(tag[..4], "")) + outerPostfix.ToString();
                    // if we detect only a end tag inside a selected line, we remove it and add an start tag to the start of a selected line
                    else if (textLines[i].Contains(tag[^5..]))
                        textLines[i] = outerPrefix.ToString() + tag[4..].Replace("{message}", textLines[i].Replace(tag[^5..], "")) + outerPostfix.ToString();
                    // if there are no tags inside, we just add new tags
                    else
                        textLines[i] = outerPrefix.ToString() + tag.Replace("{message}", textLines[i]) + outerPostfix.ToString();
                }
            }
            // joining all lines
            return string.Join("\r\n", textLines);
        }

        public static string ToStatus(this string text) => text switch
        {
            "Exception during making request" => "No connection to the server. Check your network connection and try again!",
            "Unauthorized" => "Your token is invalid. Change in configs and restart the app.",
            "Not Found" => "Your token is invalid. Change in configs and restart the app.",
            "Bad Request: chat not found" => "Your chat id is invalid. Check your configs is it trully correct in there.",
            _ => $"{text}!"
        };

        public static string FixXamlKeys(this string text) => text.Replace("&", "&amp;").Replace("<", "&lt;");

        public static string ClearLinebrakes(this string text) => text.Replace("\r", "").Replace("\n", "");
    }
}
