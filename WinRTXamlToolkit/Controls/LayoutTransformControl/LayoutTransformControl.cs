﻿// (c) Copyright Microsoft Corporation.
// This source is subject to the Microsoft Public License (Ms-PL).
// Please see http://go.microsoft.com/fwlink/?LinkID=131993 for details.
// All other rights reserved.

using System;
using System.Collections.Generic;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Markup;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Media;
using Windows.Foundation;
using WinRTXamlToolkit.Controls.Common;

namespace WinRTXamlToolkit.Controls
{
    /// <summary>
    /// Control that implements support for transformations as if applied by
    /// LayoutTransform (which does not exist in Silverlight).
    /// </summary>
    [ContentProperty(Name="Child")]
    public class LayoutTransformControl : Control
    {
        #region public FrameworkElement Child
        /// <summary>
        /// Gets or sets the single child of the LayoutTransformControl.
        /// </summary>
        /// <remarks>
        /// Corresponds to Windows Presentation Foundation's Decorator.Child 
        /// property.
        /// </remarks>
        public FrameworkElement Child
        {
            get { return (FrameworkElement)GetValue(ChildProperty); }
            set { SetValue(ChildProperty, value); }
        }

        /// <summary>
        /// Identifies the ChildProperty.
        /// </summary>
        public static readonly DependencyProperty ChildProperty = DependencyProperty.
            Register(
                "Child",
                typeof (FrameworkElement),
                typeof (LayoutTransformControl),
                new PropertyMetadata(null, ChildChanged));

        /// <summary>
        /// Handle changes to the child dependency property.
        /// </summary>
        /// <param name="o">The source of the event.</param>
        /// <param name="e">Information about the event.</param>
        private static void ChildChanged(DependencyObject o, DependencyPropertyChangedEventArgs e)
        {
            // Casts are safe because Silverlight is enforcing the types
            ((LayoutTransformControl)o).OnChildChanged((FrameworkElement)e.NewValue);
        }

        /// <summary>
        /// Updates content when the child property is changed.
        /// </summary>
        /// <param name="newContent">The new child.</param>
        private void OnChildChanged(FrameworkElement newContent)
        {
            if (null != _layoutRoot)
            {
                // Clear current child
                _layoutRoot.Children.Clear();
                if (null != newContent)
                {
                    // Add the new child to the tree
                    _layoutRoot.Children.Add(newContent);
                }
                // New child means re-layout is necessary
                InvalidateMeasure();
            }
        }
        #endregion public FrameworkElement Child

        #region public Transform Transform
        /// <summary>
        /// Gets or sets the Transform of the LayoutTransformControl.
        /// </summary>
        /// <remarks>
        /// Corresponds to UIElement.RenderTransform.
        /// </remarks>
        public Transform Transform
        {
            get { return (Transform)GetValue(TransformProperty); }
            set { SetValue(TransformProperty, value); }
        }

        /// <summary>
        /// Identifies the TransformProperty dependency property.
        /// </summary>
        public static readonly DependencyProperty TransformProperty = DependencyProperty
            .Register(
                "Transform",
                typeof (Transform),
                typeof (LayoutTransformControl),
                new PropertyMetadata(null, TransformChanged));

        /// <summary>
        /// Handles changes to the Transform DependencyProperty.
        /// </summary>
        /// <param name="o">The source of the event.</param>
        /// <param name="e">Information about the event.</param>
        private static void TransformChanged(DependencyObject o, DependencyPropertyChangedEventArgs e)
        {
            // Casts are safe because Silverlight is enforcing the types
            ((LayoutTransformControl)o).OnTransformChanged(
                e.OldValue as Transform,
                (Transform)e.NewValue);
        }

        /// <summary>
        /// Processes the transform when the transform is changed.
        /// </summary>
        /// <param name="oldValue">The old transform</param>
        /// <param name="newValue">The transform to process.</param>
        private void OnTransformChanged(Transform oldValue, Transform newValue)
        {
            if (oldValue != null)
            {
                UnsubscribeFromTransformPropertyChanges(oldValue);
            }

            if (newValue != null)
            {
                SubscribeToTransformPropertyChanges(newValue);
            }

            ProcessTransform();
        }
        #endregion

        #region Private fields
        /// <summary>
        /// Value used to work around double arithmetic rounding issues in 
        /// Silverlight.
        /// </summary>
        private const double AcceptableDelta = 0.0001;

        /// <summary>
        /// Value used to work around double arithmetic rounding issues in 
        /// Silverlight.
        /// </summary>
        private const int DecimalsAfterRound = 4;

        /// <summary>
        /// Host panel for Child element.
        /// </summary>
        private Panel _layoutRoot;

        /// <summary>
        /// RenderTransform/MatrixTransform applied to layout root.
        /// </summary>
        private MatrixTransform _matrixTransform;

        /// <summary>
        /// Transformation matrix corresponding to matrix transform.
        /// </summary>
        private Matrix _transformation;

        /// <summary>
        /// Actual DesiredSize of Child element.
        /// </summary>
        private Size _childActualSize = Size.Empty;

        /// <summary>
        /// List of property change event sources for events when properties of the Transform tree change
        /// </summary>
        private readonly Dictionary<Transform, List<PropertyChangeEventSource<double>>>
            _transformPropertyChangeEventSources = new Dictionary
                <Transform, List<PropertyChangeEventSource<double>>>();
        #endregion

        #region CTOR
        /// <summary>
        /// Initializes a new instance of the LayoutTransformControl class.
        /// </summary>
        public LayoutTransformControl()
        {
            // Can't tab to LayoutTransformControl
            IsTabStop = false;

            // Disable layout rounding because its rounding of values confuses 
            // things.
            UseLayoutRounding = false;

            // Hard coded template is never meant to be changed and avoids the 
            // need for generic.xaml.
            const string templateXaml = @"<ControlTemplate " +
                                        "xmlns='http://schemas.microsoft.com/winfx/2006/xaml/presentation' " +
                                        "xmlns:x='http://schemas.microsoft.com/winfx/2006/xaml'>" +
                                        "<Grid x:Name='LayoutRoot' Background='{TemplateBinding Background}'>" +
                                        "<Grid.RenderTransform>" +
                                        "<MatrixTransform x:Name='MatrixTransform'/>" +
                                        "</Grid.RenderTransform>" +
                                        "</Grid>" +
                                        "</ControlTemplate>";
            Template = (ControlTemplate)XamlReader.Load(templateXaml);
        } 
        #endregion

        #region OnApplyTemplate()
        /// <summary>
        /// Called whenever the control's template changes.
        /// </summary>
        protected override void OnApplyTemplate()
        {
            // Save existing content and remove it from the visual tree
            FrameworkElement savedContent = Child;
            Child = null;

            // Apply new template
            base.OnApplyTemplate();

            // Find template parts
            _layoutRoot = GetTemplateChild("LayoutRoot") as Panel;
            _matrixTransform = GetTemplateChild("MatrixTransform") as MatrixTransform;

            // RestoreAsync saved content
            Child = savedContent;

            // Apply the current transform
            TransformUpdated();
        } 
        #endregion

        #region UnsubscribeFromTransformPropertyChanges()
        private void UnsubscribeFromTransformPropertyChanges(Transform transform)
        {
            var propertyChangeEventSources =
                _transformPropertyChangeEventSources[transform];

            foreach (var propertyChangeEventSource in propertyChangeEventSources)
            {
                propertyChangeEventSource.ValueChanged -= OnTransformPropertyChanged;
            }

            _transformPropertyChangeEventSources.Remove(transform);
        } 
        #endregion

        #region SubscribeToTransformPropertyChanges()
        private void SubscribeToTransformPropertyChanges(Transform transform)
        {
            var transformGroup = transform as TransformGroup;

            if (transformGroup != null)
            {
                foreach (var childTransform in transformGroup.Children)
                {
                    SubscribeToTransformPropertyChanges(childTransform);
                }

                return;
            }

            var propertyChangeEventSources =
                new List<PropertyChangeEventSource<double>>();
            _transformPropertyChangeEventSources.Add(transform, propertyChangeEventSources);
            var rotateTransform = transform as RotateTransform;

            if (rotateTransform != null)
            {
                var anglePropertyChangeEventSource =
                    new PropertyChangeEventSource<double>(
                        rotateTransform,
                        "Angle");
                anglePropertyChangeEventSource.ValueChanged +=
                    OnTransformPropertyChanged;
                propertyChangeEventSources.Add(anglePropertyChangeEventSource);
                return;
            }

            var scaleTransform = transform as ScaleTransform;

            if (scaleTransform != null)
            {
                var scaleXPropertyChangeEventSource =
                    new PropertyChangeEventSource<double>(
                        scaleTransform,
                        "ScaleX");
                scaleXPropertyChangeEventSource.ValueChanged +=
                    OnTransformPropertyChanged;
                propertyChangeEventSources.Add(scaleXPropertyChangeEventSource);
                var scaleYPropertyChangeEventSource =
                    new PropertyChangeEventSource<double>(
                        scaleTransform,
                        "ScaleY");
                scaleYPropertyChangeEventSource.ValueChanged +=
                    OnTransformPropertyChanged;
                propertyChangeEventSources.Add(scaleYPropertyChangeEventSource);
                return;
            }

            var skewTransform = transform as SkewTransform;

            if (skewTransform != null)
            {
                var angleXPropertyChangeEventSource =
                    new PropertyChangeEventSource<double>(
                        skewTransform,
                        "AngleX");
                angleXPropertyChangeEventSource.ValueChanged +=
                    OnTransformPropertyChanged;
                propertyChangeEventSources.Add(angleXPropertyChangeEventSource);
                var angleYPropertyChangeEventSource =
                    new PropertyChangeEventSource<double>(
                        skewTransform,
                        "AngleY");
                angleYPropertyChangeEventSource.ValueChanged +=
                    OnTransformPropertyChanged;
                propertyChangeEventSources.Add(angleYPropertyChangeEventSource);
                return;
            }

            var matrixTransform = transform as MatrixTransform;

            if (matrixTransform != null)
            {
                var matrixPropertyChangeEventSource =
                    new PropertyChangeEventSource<double>(
                        matrixTransform,
                        "Matrix");
                matrixPropertyChangeEventSource.ValueChanged +=
                    OnTransformPropertyChanged;
                propertyChangeEventSources.Add(matrixPropertyChangeEventSource);
            }
        } 
        #endregion

        #region OnTransformPropertyChanged()
        /// <summary>
        /// Called when a property of a Transform changes.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void OnTransformPropertyChanged(object sender, double e)
        {
            TransformUpdated();
        } 
        #endregion

        #region TransformUpdated()
        /// <summary>
        /// Notifies the LayoutTransformControl that some aspect of its 
        /// Transform property has changed.
        /// </summary>
        /// <remarks>
        /// Call this to update the LayoutTransform in cases where 
        /// LayoutTransformControl wouldn't otherwise know to do so.
        /// </remarks>
        public void TransformUpdated()
        {
            ProcessTransform();
        } 
        #endregion

        #region ProcessTransform()
        /// <summary>
        /// Processes the current transform to determine the corresponding 
        /// matrix.
        /// </summary>
        private void ProcessTransform()
        {
            // Get the transform matrix and apply it
            _transformation = RoundMatrix(GetTransformMatrix(Transform), DecimalsAfterRound);

            if (null != _matrixTransform)
            {
                _matrixTransform.Matrix = _transformation;
            }

            // New transform means re-layout is necessary
            InvalidateMeasure();
        } 
        #endregion

        #region GetTransformMatrix()
        /// <summary>
        /// Walks the Transform and returns the corresponding matrix.
        /// </summary>
        /// <param name="transform">The transform to create a matrix for.
        /// </param>
        /// <returns>The matrix calculated from the transform.</returns>
        private Matrix GetTransformMatrix(Transform transform)
        {
            if (null != transform)
            {
                // WPF equivalent of this entire method (why oh why only WPF...):
                // return transform.Value;

                // Process the TransformGroup
                var transformGroup = transform as TransformGroup;

                if (null != transformGroup)
                {
                    var groupMatrix = Matrix.Identity;

                    foreach (var child in transformGroup.Children)
                    {
                        groupMatrix = MatrixMultiply(groupMatrix, GetTransformMatrix(child));
                    }

                    return groupMatrix;
                }

                // Process the RotateTransform
                var rotateTransform = transform as RotateTransform;

                if (null != rotateTransform)
                {
                    var angle = rotateTransform.Angle;
                    var angleRadians = (2 * Math.PI * angle) / 360;
                    var sine = Math.Sin(angleRadians);
                    var cosine = Math.Cos(angleRadians);

                    return new Matrix(cosine, sine, -sine, cosine, 0, 0);
                }

                // Process the ScaleTransform
                var scaleTransform = transform as ScaleTransform;

                if (null != scaleTransform)
                {
                    var scaleX = scaleTransform.ScaleX;
                    var scaleY = scaleTransform.ScaleY;

                    return new Matrix(scaleX, 0, 0, scaleY, 0, 0);
                }

                // Process the SkewTransform
                var skewTransform = transform as SkewTransform;

                if (null != skewTransform)
                {
                    var angleX = skewTransform.AngleX;
                    var angleY = skewTransform.AngleY;
                    var angleXRadians = (2 * Math.PI * angleX) / 360;
                    var angleYRadians = (2 * Math.PI * angleY) / 360;

                    return new Matrix(1, angleYRadians, angleXRadians, 1, 0, 0);
                }

                // Process the MatrixTransform
                MatrixTransform matrixTransform = transform as MatrixTransform;
                if (null != matrixTransform)
                {
                    return matrixTransform.Matrix;
                }

                if (transform is CompositeTransform)
                {
                    throw new NotSupportedException("CompositeTransforms are not supported (yet) by the LayoutTransformControl.");
                }

                // TranslateTransform has no effect in LayoutTransform
            }

            // Fall back to no-op transformation
            return Matrix.Identity;
        } 
        #endregion

        #region MeasureOverride()
        /// <summary>
        /// Provides the behavior for the "Measure" pass of layout.
        /// </summary>
        /// <param name="availableSize">The available size that this element can
        /// give to child elements. Infinity can be specified as a value to 
        /// indicate that the element will size to whatever content is 
        /// available.</param>
        /// <returns>The size that this element determines it needs during 
        /// layout, based on its calculations of child element sizes.</returns>
        protected override Size MeasureOverride(Size availableSize)
        {
            FrameworkElement child = Child;
            if ((null == _layoutRoot) || (null == child))
            {
                // No content, no size
                return Size.Empty;
            }

            Size measureSize;
            if (_childActualSize == Size.Empty)
            {
                // Determine the largest size after the transformation
                measureSize = ComputeLargestTransformedSize(availableSize);
            }
            else
            {
                // Previous measure/arrange pass determined that 
                // Child.DesiredSize was larger than believed.
                measureSize = _childActualSize;
            }

            // Perform a mesaure on the _layoutRoot (containing Child)
            _layoutRoot.Measure(measureSize);

            // WPF equivalent of _childActualSize technique (much simpler, but 
            // doesn't work on Silverlight 2). If the child is going to render 
            // larger than the available size, re-measure according to that size.
            ////child.Arrange(new Rect());
            ////if (child.RenderSize != child.DesiredSize)
            ////{
            ////    _layoutRoot.Measure(child.RenderSize);
            ////}

            // Transform DesiredSize to find its width/height
            Rect transformedDesiredRect = RectTransform(new Rect(0, 0, _layoutRoot.DesiredSize.Width, _layoutRoot.DesiredSize.Height), _transformation);
            Size transformedDesiredSize = new Size(transformedDesiredRect.Width, transformedDesiredRect.Height);

            // Return result to allocate enough space for the transformation
            return transformedDesiredSize;
        } 
        #endregion

        #region ArrangeOverride()
        /// <summary>
        /// Provides the behavior for the "Arrange" pass of layout.
        /// </summary>
        /// <param name="finalSize">The final area within the parent that this 
        /// element should use to arrange itself and its children.</param>
        /// <returns>The actual size used.</returns>
        /// <remarks>
        /// Using the WPF paramater name finalSize instead of Silverlight's 
        /// finalSize for clarity.
        /// </remarks>
        protected override Size ArrangeOverride(Size finalSize)
        {
            FrameworkElement child = Child;
            if ((null == _layoutRoot) || (null == child))
            {
                // No child, use whatever was given
                return finalSize;
            }

            // Determine the largest available size after the transformation
            Size finalSizeTransformed = ComputeLargestTransformedSize(finalSize);
            if (IsSizeSmaller(finalSizeTransformed, _layoutRoot.DesiredSize))
            {
                // Some elements do not like being given less space than they asked for (ex: TextBlock)
                // Bump the working size up to do the right thing by them
                finalSizeTransformed = _layoutRoot.DesiredSize;
            }

            // Transform the working size to find its width/height
            Rect transformedRect = RectTransform(new Rect(0, 0, finalSizeTransformed.Width, finalSizeTransformed.Height), _transformation);
            // Create the Arrange rect to center the transformed content
            Rect finalRect = new Rect(
                -transformedRect.Left + ((finalSize.Width - transformedRect.Width) / 2),
                -transformedRect.Top + ((finalSize.Height - transformedRect.Height) / 2),
                finalSizeTransformed.Width,
                finalSizeTransformed.Height);

            // Perform an Arrange on _layoutRoot (containing Child)
            _layoutRoot.Arrange(finalRect);

            // This is the first opportunity under Silverlight to find out the Child's true DesiredSize
            if (IsSizeSmaller(finalSizeTransformed, child.RenderSize) && (Size.Empty == _childActualSize))
            {
                // Unfortunately, all the work so far is invalid because the wrong DesiredSize was used
                // Make a note of the actual DesiredSize
                _childActualSize = new Size(child.ActualWidth, child.ActualHeight);

                // Force a new measure/arrange pass
                InvalidateMeasure();
            }
            else
            {
                // Clear the "need to measure/arrange again" flag
                _childActualSize = Size.Empty;
            }

            // Return result to perform the transformation
            return finalSize;
        } 
        #endregion

        #region ComputeLargestTransformedSize()
        /// <summary>
        /// Computes the largest usable size after applying the transformation 
        /// to the specified bounds.
        /// </summary>
        /// <param name="arrangeBounds">The size to arrange within.</param>
        /// <returns>The size required.</returns>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Maintainability", "CA1502:AvoidExcessiveComplexity", Justification = "Closely corresponds to WPF's FrameworkElement.FindMaximalAreaLocalSpaceRect.")]
        private Size ComputeLargestTransformedSize(Size arrangeBounds)
        {
            // Computed largest transformed size
            Size computedSize = Size.Empty;

            // Detect infinite bounds and constrain the scenario
            bool infiniteWidth = double.IsInfinity(arrangeBounds.Width);

            if (infiniteWidth)
            {
                arrangeBounds.Width = arrangeBounds.Height;
            }

            bool infiniteHeight = double.IsInfinity(arrangeBounds.Height);

            if (infiniteHeight)
            {
                arrangeBounds.Height = arrangeBounds.Width;
            }

            // Capture the matrix parameters
            double a = _transformation.M11;
            double b = _transformation.M12;
            double c = _transformation.M21;
            double d = _transformation.M22;

            // Compute maximum possible transformed width/height based on starting width/height
            // These constraints define two lines in the positive x/y quadrant
            double maxWidthFromWidth = Math.Abs(arrangeBounds.Width / a);
            double maxHeightFromWidth = Math.Abs(arrangeBounds.Width / c);
            double maxWidthFromHeight = Math.Abs(arrangeBounds.Height / b);
            double maxHeightFromHeight = Math.Abs(arrangeBounds.Height / d);

            // The transformed width/height that maximize the area under each segment is its midpoint
            // At most one of the two midpoints will satisfy both constraints
            double idealWidthFromWidth = maxWidthFromWidth / 2;
            double idealHeightFromWidth = maxHeightFromWidth / 2;
            double idealWidthFromHeight = maxWidthFromHeight / 2;
            double idealHeightFromHeight = maxHeightFromHeight / 2;

            // Compute slope of both constraint lines
            double slopeFromWidth = -(maxHeightFromWidth / maxWidthFromWidth);
            double slopeFromHeight = -(maxHeightFromHeight / maxWidthFromHeight);

            if (0 == arrangeBounds.Width || 0 == arrangeBounds.Height)
            {
                // Check for empty bounds
                computedSize = new Size(0, 0);
            }
            else if (infiniteWidth && infiniteHeight)
            {
                // Check for completely unbound scenario
                computedSize = new Size(double.PositiveInfinity, double.PositiveInfinity);
            }
            else if (!MatrixHasInverse(_transformation))
            {
                // Check for singular matrix
                computedSize = new Size(0, 0);
            }
            else if ((0 == b) || (0 == c))
            {
                // Check for 0/180 degree special cases
                double maxHeight = (infiniteHeight ? double.PositiveInfinity : maxHeightFromHeight);
                double maxWidth = (infiniteWidth ? double.PositiveInfinity : maxWidthFromWidth);

                if ((0 == b) && (0 == c))
                {
                    // No constraints
                    computedSize = new Size(maxWidth, maxHeight);
                }
                else if (0 == b)
                {
                    // Constrained by width
                    double computedHeight = Math.Min(idealHeightFromWidth, maxHeight);
                    computedSize = new Size(
                        maxWidth - Math.Abs((c * computedHeight) / a),
                        computedHeight);
                }
                else if (0 == c)
                {
                    // Constrained by height
                    double computedWidth = Math.Min(idealWidthFromHeight, maxWidth);
                    computedSize = new Size(
                        computedWidth,
                        maxHeight - Math.Abs((b * computedWidth) / d));
                }
            }
            else if ((0 == a) || (0 == d))
            {
                // Check for 90/270 degree special cases

                double maxWidth = (infiniteHeight ? double.PositiveInfinity : maxWidthFromHeight);
                double maxHeight = (infiniteWidth ? double.PositiveInfinity : maxHeightFromWidth);

                if ((0 == a) && (0 == d))
                {
                    // No constraints
                    computedSize = new Size(maxWidth, maxHeight);
                }
                else if (0 == a)
                {
                    // Constrained by width
                    double computedHeight = Math.Min(idealHeightFromHeight, maxHeight);
                    computedSize = new Size(
                        maxWidth - Math.Abs((d * computedHeight) / b),
                        computedHeight);
                }
                else if (0 == d)
                {
                    // Constrained by height.
                    double computedWidth = Math.Min(idealWidthFromWidth, maxWidth);
                    computedSize = new Size(
                        computedWidth,
                        maxHeight - Math.Abs((a * computedWidth) / c));
                }
            }
            else if (idealHeightFromWidth <= ((slopeFromHeight * idealWidthFromWidth) + maxHeightFromHeight))
            {
                // Check the width midpoint for viability (by being below the 
                // height constraint line).
                computedSize = new Size(idealWidthFromWidth, idealHeightFromWidth);
            }
            else if (idealHeightFromHeight <= ((slopeFromWidth * idealWidthFromHeight) + maxHeightFromWidth))
            {
                // Check the height midpoint for viability (by being below the 
                // width constraint line).
                computedSize = new Size(idealWidthFromHeight, idealHeightFromHeight);
            }
            else
            {
                // Neither midpoint is viable; use the intersection of the two 
                // constraint lines instead.

                // Compute width by setting heights equal (m1*x+c1=m2*x+c2).
                double computedWidth = (maxHeightFromHeight - maxHeightFromWidth) / (slopeFromWidth - slopeFromHeight);
                // Compute height from width constraint line (y=m*x+c; using 
                // height would give same result).
                computedSize = new Size(
                    computedWidth,
                    (slopeFromWidth * computedWidth) + maxHeightFromWidth);
            }

            return computedSize;
        } 
        #endregion

        #region IsSizeSmaller()
        /// <summary>
        /// Return true if Size a is smaller than Size b in either dimension.
        /// </summary>
        /// <param name="a">The left size.</param>
        /// <param name="b">The right size.</param>
        /// <returns>A value indicating whether the left size is smaller than
        /// the right.</returns>
        private static bool IsSizeSmaller(Size a, Size b)
        {
            // WPF equivalent of following code:
            // return ((a.Width < b.Width) || (a.Height < b.Height));
            return ((a.Width + AcceptableDelta < b.Width) || (a.Height + AcceptableDelta < b.Height));
        } 
        #endregion

        #region RoundMatrix()
        /// <summary>
        /// Rounds the non-offset elements of a matrix to avoid issues due to 
        /// floating point imprecision.
        /// </summary>
        /// <param name="matrix">The matrix to round.</param>
        /// <param name="decimalsAfterRound">The number of decimals after the
        /// round.</param>
        /// <returns>The rounded matrix.</returns>
        private static Matrix RoundMatrix(Matrix matrix, int decimalsAfterRound)
        {
            return new Matrix(
                Math.Round(matrix.M11, decimalsAfterRound),
                Math.Round(matrix.M12, decimalsAfterRound),
                Math.Round(matrix.M21, decimalsAfterRound),
                Math.Round(matrix.M22, decimalsAfterRound),
                matrix.OffsetX,
                matrix.OffsetY);
        } 
        #endregion

        #region RectTransform()
        /// <summary>
        /// Implement Windows Presentation Foundation's Rect.Transform on 
        /// Silverlight.
        /// </summary>
        /// <param name="rectangle">The rectangle to transform.</param>
        /// <param name="matrix">The matrix to use to transform the rectangle.
        /// </param>
        /// <returns>The transformed rectangle.</returns>
        private static Rect RectTransform(Rect rectangle, Matrix matrix)
        {
            // WPF equivalent of following code:
            // var rectTransformed = Rect.Transform(rect, matrix);
            Point leftTop = matrix.Transform(new Point(rectangle.Left, rectangle.Top));
            Point rightTop = matrix.Transform(new Point(rectangle.Right, rectangle.Top));
            Point leftBottom = matrix.Transform(new Point(rectangle.Left, rectangle.Bottom));
            Point rightBottom = matrix.Transform(new Point(rectangle.Right, rectangle.Bottom));
            double left = Math.Min(Math.Min(leftTop.X, rightTop.X), Math.Min(leftBottom.X, rightBottom.X));
            double top = Math.Min(Math.Min(leftTop.Y, rightTop.Y), Math.Min(leftBottom.Y, rightBottom.Y));
            double right = Math.Max(Math.Max(leftTop.X, rightTop.X), Math.Max(leftBottom.X, rightBottom.X));
            double bottom = Math.Max(Math.Max(leftTop.Y, rightTop.Y), Math.Max(leftBottom.Y, rightBottom.Y));
            Rect rectTransformed = new Rect(left, top, right - left, bottom - top);
            return rectTransformed;
        } 
        #endregion

        #region MatrixMultiply()
        /// <summary>
        /// Implements Windows Presentation Foundation's Matrix.Multiply on 
        /// Silverlight.
        /// </summary>
        /// <param name="matrix1">The left matrix.</param>
        /// <param name="matrix2">The right matrix.</param>
        /// <returns>The product of the two matrices.</returns>
        private static Matrix MatrixMultiply(Matrix matrix1, Matrix matrix2)
        {
            // WPF equivalent of following code:
            // return Matrix.Multiply(matrix1, matrix2);
            return new Matrix(
                (matrix1.M11 * matrix2.M11) + (matrix1.M12 * matrix2.M21),
                (matrix1.M11 * matrix2.M12) + (matrix1.M12 * matrix2.M22),
                (matrix1.M21 * matrix2.M11) + (matrix1.M22 * matrix2.M21),
                (matrix1.M21 * matrix2.M12) + (matrix1.M22 * matrix2.M22),
                ((matrix1.OffsetX * matrix2.M11) + (matrix1.OffsetY * matrix2.M21)) + matrix2.OffsetX,
                ((matrix1.OffsetX * matrix2.M12) + (matrix1.OffsetY * matrix2.M22)) + matrix2.OffsetY);
        } 
        #endregion

        #region MatrixHasInverse()
        /// <summary>
        /// Implements Windows Presentation Foundation's Matrix.HasInverse on
        /// Silverlight.
        /// </summary>
        /// <param name="matrix">The matrix.</param>
        /// <returns>True if matrix has an inverse.</returns>
        private static bool MatrixHasInverse(Matrix matrix)
        {
            // WPF equivalent of following code:
            // return matrix.HasInverse;
            return (0 != ((matrix.M11 * matrix.M22) - (matrix.M12 * matrix.M21)));
        } 
        #endregion
    }
}