//------------------------------------------------------------------------------
// <copyright file="VariableItem.cs">
//     Copyright (c) Adam Wulkiewicz.
// </copyright>
//------------------------------------------------------------------------------

using System.Drawing;
using System.Windows.Media.Imaging;

namespace GraphicalDebugging
{
    class VariableItem
            : System.ComponentModel.INotifyPropertyChanged
    {
        public event System.ComponentModel.PropertyChangedEventHandler PropertyChanged;
        private void NotifyPropertyChanged([System.Runtime.CompilerServices.CallerMemberName] string propertyName = "")
        {
            if (PropertyChanged != null)
            {
                PropertyChanged(this, new System.ComponentModel.PropertyChangedEventArgs(propertyName));
            }
        }

        public VariableItem(string name = null, Bitmap bmp = null, string type = null)
        {
            Name = name;
            Bmp = bmp;
            Type = type;
        }

        private string name;
        public string Name
        {
            get { return this.name; }
            set
            {
                if (value != this.name)
                {
                    this.name = value;
                    NotifyPropertyChanged("Name");
                }
            }
        }

        private Bitmap bmp;
        public Bitmap Bmp
        {
            get { return this.bmp; }
            set
            {
                this.bmp = value;
                this.bmpImg = this.bmp == null ? null : Util.BitmapToBitmapImage(this.bmp);
            }
        }

        private BitmapImage bmpImg;
        public BitmapImage BmpImg { get { return this.bmpImg; } }

        private string type;
        public string Type
        {
            get { return this.type; }
            set { this.type = value; }
        }
    }
}
