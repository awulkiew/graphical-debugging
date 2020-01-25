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
            Error = null;
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

        public bool IsError
        {
            get { return Error != null && Error != ""; }
        }

        public string TypeOrError
        {
            get { return IsError ? Error : Type; }
        }
    }

    class DrawableItem : VariableItem
    {
        public DrawableItem()
        { }

        public DrawableItem(ExpressionDrawer.IDrawable drawable,
                            Geometry.Traits traits,
                            string name, string type)
            : base(name, type)
        {
            Drawable = drawable;
            Traits = traits;
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

    class GraphicalItem : DrawableItem
    {
        public GraphicalItem()
        { }

        public GraphicalItem(ExpressionDrawer.IDrawable drawable,
                             Geometry.Traits traits,
                             string name, System.Drawing.Bitmap bmp, string type, string error)
            : base(drawable, traits, name, type)
        {
            Error = error;
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

    class ColoredDrawableItem : DrawableItem
    {
        public ColoredDrawableItem(int colorId, Colors colors)
        {
            SetColor(colorId, colors);
        }

        public ColoredDrawableItem(ExpressionDrawer.IDrawable drawable,
                                   Geometry.Traits traits,
                                   string name, string type, int colorId, Colors colors)
            : base(drawable, traits, name, type)
        {
            SetColor(colorId, colors);
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
    }

    class GeometryItem : ColoredDrawableItem
    {
        public GeometryItem(int colorId, Colors colors)
            : base(colorId, colors)
        { }

        public GeometryItem(ExpressionDrawer.IDrawable drawable,
                        Geometry.Traits traits,
                        string name, string type, int colorId, Colors colors)
            : base(drawable, traits, name, type, colorId, colors)
        { }
    }

    class PlotItem : ColoredDrawableItem
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
