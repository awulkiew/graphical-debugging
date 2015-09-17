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
        public GeometryItem(System.Drawing.Color color)
            : base()
        {
            Color = Util.ConvertColor(color);
        }

        public GeometryItem(string name, /*ExpressionDrawer.IDrawable drawable,*/ string type, Color color)
            : base(name, null, type)
        {
            Color = color;
            //Drawable = drawable;
        }

        private Color color;
        public Color Color
        {
            get { return this.color; }
            set { this.color = value; }
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
