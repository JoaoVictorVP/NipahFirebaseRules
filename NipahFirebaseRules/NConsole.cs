using System.Text;

public static class NConsole
{
    static NStringBuilder text = new NStringBuilder();
    // static StringBuilder text = new StringBuilder(320);

    public static ConsoleColor ForegroundColor
    {
        get => Console.ForegroundColor;
        set
        {
            Console.ForegroundColor = value;
            text.ForegroundColor = value;
        }
    }
    public static ConsoleColor BackgroundColor
    {
        get => Console.BackgroundColor;
        set
        {
            Console.BackgroundColor = value;
            text.BackgroundColor = value;
        }
    }

    public static void ResetColor()
    {
        Console.ResetColor();
        text.ForegroundColor = Console.ForegroundColor;
        text.BackgroundColor = Console.BackgroundColor;
    }

    public static void Write(object value)
    {
        string val = (value != null ? value.ToString() : "[NULL]")!;
        text.Append(val);
        Console.Write(val);
    }
    public static void WriteLine()
    {
        text.AppendLine();
        Console.WriteLine();
    }
    public static void WriteLine(object value)
    {
        string val = (value != null ? value.ToString() : "[NULL]")!;
        text.AppendLine(val);
        Console.WriteLine(val);
    }
    public static void Clear()
    {
        text.Clear();
        Console.Clear();
    }

    public static ConsoleKeyInfo ReadKey(bool intercept = false)
    {
        var key = Console.ReadKey(intercept);
        if (!intercept)
            text.Append(key.KeyChar);
        return key;
    }

    public static string ReadLine()
    {
        string line = Console.ReadLine();
        text.AppendLine(line);
        return line;
    }

    public static void Options(params (string message, Action callback)[] options)
    {
        int count = options.Length;
        if (count == 0) throw new Exception("Expecting more than zero items on 'options'");

        int selected = 0;

        void draw(bool perma = false)
        {
            for (int i = 0; i < count; i++)
            {
                if (selected == i)
                    Console.WriteLine(">> " + options[i].message);
                else
                    Console.WriteLine("   " + options[i].message);

                if(perma)
                {
                    if (selected == i)
                        text.AppendLine(">> " + options[i].message);
                    else
                        text.AppendLine("   " + options[i].message);
                }
            }

            if(!perma)
                input();
        }

        draw();

        void clear()
        {
            Console.Clear();
        }

        void go(int direction)
        {
            selected += direction;
            if (selected < 0)
                selected = count - 1;
            else if (selected >= count)
                selected = 0;

            clear();

            text.Print();

            draw();
        }

        void invokeSelected()
        {
            clear();
            text.Print();
            draw(true);

            options[selected].callback();
        }

        void input()
        {
            var press = Console.ReadKey(true).Key;

            switch (press)
            {
                case ConsoleKey.Enter: invokeSelected(); break;
                case ConsoleKey.UpArrow: go(1); break;
                case ConsoleKey.DownArrow: go(-1); break;

                default: go(0); break;
            }
        }
    }
}

public class NStringBuilder
{
    List<(string content, ConsoleColor fcolor, ConsoleColor bcolor)> inputs = new (320);

    public ConsoleColor ForegroundColor, BackgroundColor;

    public void Append(char character)
    {
        inputs.Add((character.ToString(), ForegroundColor, BackgroundColor));
    }

    public void Append(string text)
    {
        inputs.Add((text, ForegroundColor, BackgroundColor));
    }
    public void AppendLine()
    {
        inputs.Add(("\n", ForegroundColor, BackgroundColor));
    }
    public void AppendLine(string text)
    {
        inputs.Add((text + '\n', ForegroundColor, BackgroundColor));
    }

    public void Clear()
    {
        inputs.Clear();
    }

    public void Print()
    {
        var defFColor = Console.ForegroundColor;
        var defBColor = Console.BackgroundColor;

        foreach(var (text, fcolor, bcolor) in inputs)
        {
            Console.ForegroundColor = fcolor;
            Console.BackgroundColor = bcolor;
            Console.Write(text);
        }

        Console.ForegroundColor = defFColor;
        Console.BackgroundColor = defBColor;
    }
}