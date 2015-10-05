//------------------------------------------------------------------------------
// <copyright file="GeometryItem.cs">
//     Copyright (c) Adam Wulkiewicz.
// </copyright>
//------------------------------------------------------------------------------

using System.Windows.Media;

namespace GraphicalDebugging
{
    class GeometryItem : VariableItem
    {
        public GeometryItem(int colorId, Colors colors)
            : base()
        {
            SetColor(colorId, colors);
        }

        public GeometryItem(string name, /*ExpressionDrawer.IDrawable drawable,*/ string type, int colorId, Colors colors)
            : base(name, null, type)
        {
            SetColor(colorId, colors);
            //Drawable = drawable;
        }

        private int colorId;
        public int ColorId
        {
            get { return colorId; }
        }

        private Color color;
        public Color Color
        {
            get { return color; }
        }

        private void SetColor(int colorId, Colors colors)
        {
            this.colorId = colorId;
            color = Util.ConvertColor(colors[colorId]);
        }

        /*
        private ExpressionDrawer.IDrawable drawable;
        public ExpressionDrawer.IDrawable Drawable
        {
            get { return this.drawable; }
            set { this.drawable = value; }
        }*/
    }
}
