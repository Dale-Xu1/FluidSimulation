using System;
using System.Windows.Input;
using System.Windows.Media;

using FluidSimulation.Renderer;
using SharpDX;

namespace FluidSimulation;

using System.Windows;

public partial class MainWindow : Window
{

    private const double SCALE = 1.5;


    private FluidRenderer renderer = null!;

    public MainWindow() => InitializeComponent();

    private (int, int) Dimensions => ((int) (content.ActualWidth * SCALE), (int) (content.ActualHeight * SCALE));
    private void Start(object sender, RoutedEventArgs e)
    {
        (int width, int height) = Dimensions;
        renderer = new FluidRenderer(this, width, height);

        CompositionTarget.Rendering += (object? sender, EventArgs e) => renderer.Render();
    }


    private bool down = false;
    private Vector2 previous;

    private Color3 color;

    private void Down(object sender, MouseEventArgs e)
    {
        down = true;

        Random random = new Random();
        color = new Color3(random.NextFloat(0, 1), random.NextFloat(0, 1), random.NextFloat(0, 1));
    }
    private void Up(object sender, MouseEventArgs e) => down = false;

    private void Move(object sender, MouseEventArgs e)
    {
        Point point = e.GetPosition(content);
        float x = (float) (point.X * SCALE), y = (float) (point.Y * SCALE);

        Vector2 current = new(x, y);
        if (down) renderer.AddForce(current, previous, color);

        previous = current;
    }

}

