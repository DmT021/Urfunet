using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

namespace Dashboard
{
    /// <summary>
    /// Interaction logic for Prompt.xaml
    /// </summary>
    public partial class Prompt : Window
    {
        public string Text { get; private set; }

        public Prompt(string caption, string text)
        {
            InitializeComponent();
            Title = caption;
            textBox.Text = text;
        }

        public static string ShowDialog(string caption, string text)
        {
            Prompt prompt = new Prompt(caption, text);
            prompt.ShowDialog();
            return prompt.Text;
        }

        private void okButton_Click(object sender, RoutedEventArgs e)
        {
            Text = textBox.Text;
            Close();
        }

        private void cancelButton_Click(object sender, RoutedEventArgs e)
        {
            Text = null;
            Close();
        }
    }
}
