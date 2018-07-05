﻿using System;
using System.Text;

namespace LunarParser.JSON
{
    public class JSONReader
    {
        private enum State
        {
            Type,
            Name,
            Colon,
            Value,
            Next
        }

        private enum InputMode
        {
            None,
            Text,
            Number
        }

        public static DataNode ReadFromString(string contents)
        {
            int index = 0;
            var root = ReadNode(contents, ref index, null);
            return root;
        }

        private static void ReadString(string target, string contents, ref int index)
        {
            index--;
            for (int i=0; i<target.Length; i++)
            {
                if (index >= contents.Length)
                {
                    throw new Exception($"JSON parsing exception, unexpected end of data");
                }

                var c = contents[index];
                
                if (c != target[i])
                {
                    throw new Exception($"JSON parsing exception, unexpected character");
                }

                index++;
            }
        }

        private static DataNode ReadNode(string contents, ref int index, string name)
        {
            DataNode result = null;

            var state = State.Type;
            char c;
            var mode = InputMode.None;

            StringBuilder name_content = new StringBuilder();
            StringBuilder value_content = new StringBuilder();

            int rewind_index = index;

            bool is_escaped = false;

            do
            {
                bool isWhiteSpace;
                bool next = false;
                do
                {
                    if (index >= contents.Length)
                    {
                        throw new Exception($"JSON parsing exception, unexpected end of data");
                    }

                    c = contents[index];
                    isWhiteSpace = Char.IsWhiteSpace(c);

                    if (!isWhiteSpace)
                    {
                        rewind_index = index;
                    }

                    index++;


                    next = (mode == InputMode.None) ? isWhiteSpace : false;
                } while (next);

                switch (state)
                {
                    case State.Type:
                        {
                            switch (c)
                            {
                                case '{':
                                    {
                                        result = DataNode.CreateObject(name);
                                        state = State.Name;
                                        break;
                                    }

                                case '[':
                                    {
                                        result = DataNode.CreateArray(name);
                                        state = State.Value;
                                        break;
                                    }


                                default:
                                    {
                                        throw new Exception($"JSON parsing exception at {ParserUtils.GetOffsetError(contents, index)}, unexpected character");
                                    }
                            }
                            break;
                        }

                    case State.Name:
                        {
                            switch (c)
                            {
                                case '"':
                                    {
                                        if (mode == InputMode.None)
                                        {
                                            mode = InputMode.Text;
                                            name_content.Length = 0;
                                        }
                                        else
                                        {
                                            mode = InputMode.None;
                                            state = State.Colon;
                                        }
                                        break;
                                    }

                                default:
                                    {
                                        if (mode == InputMode.Text)
                                        {
                                            name_content.Append(c);
                                        }
                                        else
                                        {
                                            throw new Exception($"JSON parsing exception at {ParserUtils.GetOffsetError(contents, index)}, unexpected character");
                                        }
                                        break;
                                    }
                            }
                            break;
                        }

                    case State.Colon:
                        {
                            switch (c)
                            {
                                case ':':
                                    {
                                        state = State.Value;
                                        break;
                                    }

                                default:
                                    {
                                        throw new Exception($"JSON parsing exception at {ParserUtils.GetOffsetError(contents, index)}, expected collon");
                                    }
                            }
                            break;
                        }

                    case State.Value:
                        {
                            if (c == '\\' && !is_escaped)
                            {
                                is_escaped = true;
                            }
                            else
                            if (is_escaped)
                            {
                                is_escaped = false;

                                if (c == 'u')
                                {
                                    var hex = "";
                                    for (int i=0; i<4; i++)
                                    {
                                        if (index >= contents.Length)
                                        {
                                            throw new Exception($"JSON parsing exception, unexpected end of data");
                                        }
                                        hex += contents[index]; index++;
                                    }
                                    
                                    ushort unicode_val;
                                    unicode_val = ushort.Parse(hex, System.Globalization.NumberStyles.HexNumber);

                                    c = (char)unicode_val;
                                }

                                value_content.Append(c);
                            }
                            else
                            if (c == 'n' && mode == InputMode.None)
                            {
                                ReadString("null", contents, ref index);
                                result.AddField(name_content.ToString(), "");
                                state = State.Next;
                            }
                            else
                            if (c == 'f' && mode == InputMode.None)
                            {
                                ReadString("false", contents, ref index);
                                result.AddField(name_content.ToString(), "false");
                                state = State.Next;
                            }
                            else
                            if (c == 't' && mode == InputMode.None)
                            {
                                ReadString("true", contents, ref index);
                                result.AddField(name_content.ToString(), "true");
                                state = State.Next;                            
                            }
                            else
                            if (c == ']' && mode == InputMode.None && result.Kind == NodeKind.Array)
                            {
                                return result;
                            }
                            else
                            switch (c)
                            {
                                case '"':
                                    {
                                        if (mode == InputMode.None)
                                        {
                                            mode = InputMode.Text;
                                            value_content.Length = 0;
                                        }
                                        else
                                        {
                                            mode = InputMode.None;
                                            result.AddField(name_content.ToString(), value_content.ToString());
                                            state = State.Next;
                                        }
                                        break;
                                    }

                                case '[':
                                case '{':
                                    {
                                        index = rewind_index;
                                        var node = ReadNode(contents, ref index, name_content.ToString());
                                        result.AddNode(node);

                                        state = State.Next;
                                        break;
                                    }

                                default:
                                    {
                                        if (mode == InputMode.Text)
                                        {
                                            value_content.Append(c);
                                        }
                                        else
                                        if (char.IsNumber(c) || c == '+' || c == '-' || (mode == InputMode.Number && (c =='.' || c == 'E' || c == 'e')))
                                        {
                                            if (mode != InputMode.Number)
                                            {
                                                value_content.Length = 0;
                                                mode = InputMode.Number;
                                            }

                                            value_content.Append(c);
                                        }
                                        else
                                        {
                                            if (mode == InputMode.Number)
                                            {
                                                mode = InputMode.None;
                                                result.AddField(name_content.ToString(), value_content.ToString());
                                                state = State.Next;

                                                if (c == ',' || c == ']')
                                                {
                                                    index = rewind_index;
                                                }
                                            }
                                            else
                                            {
                                                throw new Exception($"JSON parsing exception at {ParserUtils.GetOffsetError(contents, index)}, unexpected character");
                                            }

                                        }
                                        break;
                                    }
                            }
                            break;
                        }

                    case State.Next:
                        {
                            switch (c)
                            {
                                case ',':
                                    {
                                        state = result.Kind == NodeKind.Array ? State.Value: State.Name;
                                        break;
                                    }

                                case '}':
                                    {
                                        if (result.Kind != NodeKind.Object)
                                        {
                                            throw new Exception($"JSON parsing exception at {ParserUtils.GetOffsetError(contents, index)}, unexpected }}");
                                        }

                                        return result;
                                    }

                                case ']':
                                    {
                                        if (result.Kind != NodeKind.Array)
                                        {
                                            throw new Exception($"JSON parsing exception at {ParserUtils.GetOffsetError(contents, index)}, unexpected ]");
                                        }

                                        return result;
                                    }

                                default:
                                    {
                                        throw new Exception($"JSON parsing exception at {ParserUtils.GetOffsetError(contents, index)}, expected collon");
                                    }
                            }
                            break;
                        }

                }

            } while (true);
        }
    }
}
