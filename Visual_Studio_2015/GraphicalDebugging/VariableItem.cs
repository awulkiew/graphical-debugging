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
        private void NotifyPropertyChanged([System.Runtime.CompilerServices.CallerMemberName] string propertyName = "")
        {
            if (PropertyChanged != null)
                PropertyChanged(this, new System.ComponentModel.PropertyChangedEventArgs(propertyName));
        }

        public VariableItem()
        { }

        public VariableItem(string name, string type)
        {
            Name = name;
            Type = type;
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
    }

    class GraphicalItem : VariableItem
    {
        public GraphicalItem()
        { }

        public GraphicalItem(string name, System.Drawing.Bitmap bmp, string type)
            : base(name, type)
        {
            Bmp = bmp;
        }

        protected System.Drawing.Bitmap bmp;
        public System.Drawing.Bitmap Bmp
        {
            get { return this.bmp; }
            set
            {
                this.bmp = value;
                this.bmpImg = this.bmp == null ? null : Util.BitmapToBitmapImage(this.bmp);
            }
        }

        protected System.Windows.Media.Imaging.BitmapImage bmpImg;
        public System.Windows.Media.Imaging.BitmapImage BmpImg
        {
            get { return this.bmpImg; }
        }
    }

    class GeometryItem : VariableItem
    {
        public GeometryItem(int colorId, Colors colors)
        {
            SetColor(colorId, colors);
        }

        public GeometryItem(ExpressionDrawer.IDrawable drawable,
                            Geometry.Traits traits,
                            string name, string type, int colorId, Colors colors)
            : base(name, type)
        {
            SetColor(colorId, colors);
            Drawable = drawable;
            Traits = traits;
        }

        protected int colorId;
        public int ColorId
        {
            get { return colorId; }
        }

        protected System.Windows.Media.Color color;
        public System.Windows.Media.Color Color
        {
            get { return color; }
        }

        protected void SetColor(int colorId, Colors colors)
        {
            this.colorId = colorId;
            this.color = Util.ConvertColor(colors[colorId]);
        }

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

    class PlotItem : GeometryItem
    {
        public PlotItem(int colorId, Colors colors)
            : base(colorId, colors)
        { }

        public PlotItem(ExpressionDrawer.IDrawable drawable,
                        Geometry.Traits traits,
                        string name, string type, int colorId, Colors colors)
            : base(drawable, traits, name, type, colorId, colors)
        { }
    }
}
