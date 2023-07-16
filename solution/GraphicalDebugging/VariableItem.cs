//------------------------------------------------------------------------------
// <copyright file="VariableItem.cs">
//     Copyright (c) Adam Wulkiewicz.
// </copyright>
//------------------------------------------------------------------------------

namespace GraphicalDebugging
{
    class VariableItem
        : System.ComponentModel.INotifyPropertyChanged
    {
        public event System.ComponentModel.PropertyChangedEventHandler PropertyChanged;
        protected void NotifyPropertyChanged([System.Runtime.CompilerServices.CallerMemberName] string propertyName = "")
        {
            if (PropertyChanged != null)
                PropertyChanged(this, new System.ComponentModel.PropertyChangedEventArgs(propertyName));
        }

        protected string name;
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

        protected string type;
        public string Type
        {
            get { return this.type; }
            set { this.type = value; }
        }

        protected string error;
        public string Error
        {
            get { return this.error; }
            set { this.error = value; }
        }

        protected bool isEnabled = true;
        public bool IsEnabled
        {
            get { return this.isEnabled; }
            set
            {
                if (value != this.isEnabled)
                {
                    this.isEnabled = value;
                    NotifyPropertyChanged("IsEnabled");
                }
            }
        }

        public string TypeOrError
        {
            get { return string.IsNullOrEmpty(Type) ? Error : Type; }
        }

        public string ErrorOrType
        {
            get { return string.IsNullOrEmpty(Error) ? Type : Error; }
        }

        public bool IsError
        {
            get { return !string.IsNullOrEmpty(Error); }
        }

        public bool IsTypeAndError
        {
            get { return !string.IsNullOrEmpty(Type) && !string.IsNullOrEmpty(Error); }
        }
    }

    class DrawableItem : VariableItem
    {
        protected ExpressionDrawer.IDrawable drawable;
        public ExpressionDrawer.IDrawable Drawable
        {
            get { return this.drawable; }
            set { this.drawable = value; }
        }

        protected Geometry.Traits traits;
        public Geometry.Traits Traits
        {
            get { return this.traits; }
            set { this.traits = value; }
        }
    }

    class GraphicalItem : DrawableItem
    {
        protected System.Drawing.Bitmap bmp;
        public System.Drawing.Bitmap Bmp
        {
            get { return this.bmp; }
            set
            {
                this.bmp = value;
                this.bmpImg = null;
            }
        }

        protected System.Windows.Media.Imaging.BitmapImage bmpImg;
        public System.Windows.Media.Imaging.BitmapImage BmpImg
        {
            get
            {
                if (this.bmpImg == null && this.bmp != null)
                    this.bmpImg = Util.BitmapToBitmapImage(this.bmp);
                return this.bmpImg;
            }
        }

        public GraphicalItem ShallowCopy()
        {
            return this.MemberwiseClone() as GraphicalItem;
        }
    }

    class ColoredDrawableItem : DrawableItem
    {
        public ColoredDrawableItem()
        {
            Color = Util.ConvertColor(Colors.Transparent);
            ColorId = -1;
        }

        public System.Windows.Media.Color Color { get; set; }
        public int ColorId { get; set; }
    }

    class GeometryItem : ColoredDrawableItem
    {
        public GeometryItem ShallowCopy()
        {
            return this.MemberwiseClone() as GeometryItem;
        }
    }

    class PlotItem : ColoredDrawableItem
    {
        public PlotItem ShallowCopy()
        {
            return this.MemberwiseClone() as PlotItem;
        }
    }
}
