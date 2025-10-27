using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using TimeScriptCompiler.Lexer;

namespace TimeScriptCompiler
{
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
        }

        // ✅ Helper method to append colored text
        private void AppendColoredText(string text, Brush color, bool bold = false)
        {
            var paragraph = OutputBox.Document.Blocks.LastBlock as Paragraph;
            if (paragraph == null)
            {
                paragraph = new Paragraph();
                OutputBox.Document.Blocks.Add(paragraph);
            }

            var run = new Run(text) { Foreground = color };
            if (bold)
                run.FontWeight = FontWeights.Bold;

            paragraph.Inlines.Add(run);
        }

        private void RunButton_Click(object sender, RoutedEventArgs e)
        {
            OutputBox.Document.Blocks.Clear();
            string code = Editor.Text.Trim();

            if (string.IsNullOrWhiteSpace(code))
            {
                AppendColoredText("⚠ Please enter some TimeScript code before running.\n", Brushes.OrangeRed, true);
                return;
            }

            try
            {
                var lexer = new Lexer.Lexer(code);
                var tokens = lexer.Tokenize();

                AppendColoredText("LEXICAL ANALYSIS RESULT:\n", Brushes.LightCyan, true);
                AppendColoredText(new string('-', 60) + "\n", Brushes.Gray);

                foreach (var token in tokens)
                {
                    Brush color = Brushes.White;

                    if (token.Type.ToString().Contains("NUMBER"))
                        color = Brushes.LightGreen;
                    else if (token.Type.ToString().Contains("STRING"))
                        color = Brushes.Yellow;
                    else if (token.Type.ToString().Contains("IDENT"))
                        color = Brushes.LightGray;
                    else if (token.Type.ToString().Contains("SET") ||
                             token.Type.ToString().Contains("START") ||
                             token.Type.ToString().Contains("LOOP") ||
                             token.Type.ToString().Contains("SHOW") ||
                             token.Type.ToString().Contains("END") ||
                             token.Type.ToString().Contains("IF") ||
                             token.Type.ToString().Contains("ELSE"))
                        color = Brushes.Cyan;
                    else if (token.Type.ToString().Contains("EQ") ||
                             token.Type.ToString().Contains("LT") ||
                             token.Type.ToString().Contains("PLUS") ||
                             token.Type.ToString().Contains("MINUS"))
                        color = Brushes.LightPink;

                    AppendColoredText($"{token.Type,-15}", color, true);
                    AppendColoredText($" | '{token.Lexeme}' ", Brushes.LightGray);
                    AppendColoredText($"@ line {token.Line}, col {token.Column}\n", Brushes.DimGray);
                }

                AppendColoredText(new string('-', 60) + "\n", Brushes.Gray);
                AppendColoredText($"✅ Total Tokens: {tokens.Count}\n", Brushes.LightGreen, true);
            }
            catch (LexicalException ex)
            {
                AppendColoredText("LEXICAL ERROR:\n", Brushes.Red, true);
                AppendColoredText(ex.Message + "\n", Brushes.OrangeRed);
            }
            catch (System.Exception ex)
            {
                AppendColoredText("UNEXPECTED ERROR:\n", Brushes.Red, true);
                AppendColoredText(ex.Message + "\n", Brushes.OrangeRed);
            }
        }

        private void ClearButton_Click(object sender, RoutedEventArgs e)
        {
            Editor.Clear();
            OutputBox.Document.Blocks.Clear();
            Editor.Focus();
        }
    }
}
