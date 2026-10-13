using System.Windows;
using System.Windows.Controls;
using System.Windows.Markup;
using System.Windows.Media;

namespace GeMMeElec;

// These resources belong to the window chrome. Drawing, PDF and project colors
// deliberately never depend on them.
internal static class UiTheme
{
    public static T Bind<T>(T element, DependencyProperty property, string key) where T : FrameworkElement
    { element.SetResourceReference(property, key); return element; }

    public static void Configure(Window window, string theme)
    {
        window.Resources.MergedDictionaries.Add((ResourceDictionary)XamlReader.Parse(Styles));
        Bind(window, Control.BackgroundProperty, "Ui.Background");
        Bind(window, Control.ForegroundProperty, "Ui.Text");
        Apply(window, theme);
    }

    public static void Apply(Window window, string theme)
    {
        bool dark = theme == "dark";
        string[] keys = ["Background", "Surface", "Raised", "Border", "Text", "Muted", "Accent", "AccentSoft", "Error"];
        string[] colors = dark
            ? ["#111B26", "#1C2937", "#253546", "#43566B", "#EAF0F6", "#AEBECE", "#71D0E2", "#214553", "#FFB2A8"]
            : ["#EFF3F8", "#FFFFFF", "#F5F8FB", "#D4DFE9", "#203348", "#526B80", "#086B83", "#E4F2F7", "#AE2929"];
        for (int i = 0; i < keys.Length; i++) window.Resources["Ui." + keys[i]] = Drawing.Brush(colors[i]);
        // WPF system templates (combo popups, menu items and scrollbars) also
        // resolve their colors locally, without changing document brushes.
        window.Resources[SystemColors.WindowBrushKey] = window.Resources["Ui.Surface"];
        window.Resources[SystemColors.WindowTextBrushKey] = window.Resources["Ui.Text"];
        window.Resources[SystemColors.ControlBrushKey] = window.Resources["Ui.Raised"];
        window.Resources[SystemColors.ControlTextBrushKey] = window.Resources["Ui.Text"];
        window.Resources[SystemColors.ControlLightBrushKey] = window.Resources["Ui.Surface"];
        window.Resources[SystemColors.ControlDarkBrushKey] = window.Resources["Ui.Border"];
        window.Resources[SystemColors.GrayTextBrushKey] = window.Resources["Ui.Muted"];
        window.Resources[SystemColors.HighlightBrushKey] = window.Resources["Ui.AccentSoft"];
        window.Resources[SystemColors.HighlightTextBrushKey] = window.Resources["Ui.Text"];
    }

    private const string Styles = """
        <ResourceDictionary xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation" xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml">
          <Style TargetType="Button">
            <Setter Property="Foreground" Value="{DynamicResource Ui.Text}"/>
            <Setter Property="Background" Value="{DynamicResource Ui.Surface}"/>
            <Setter Property="BorderBrush" Value="{DynamicResource Ui.Border}"/>
            <Setter Property="BorderThickness" Value="1"/>
            <Setter Property="Padding" Value="9,6"/><Setter Property="Margin" Value="3"/>
            <Setter Property="Cursor" Value="Hand"/>
            <Setter Property="Template"><Setter.Value><ControlTemplate TargetType="Button">
              <Border x:Name="Frame" Background="{TemplateBinding Background}" BorderBrush="{TemplateBinding BorderBrush}" BorderThickness="{TemplateBinding BorderThickness}" CornerRadius="5" Padding="{TemplateBinding Padding}">
                <ContentPresenter HorizontalAlignment="{TemplateBinding HorizontalContentAlignment}" VerticalAlignment="Center" RecognizesAccessKey="True"/>
              </Border>
              <ControlTemplate.Triggers>
                <Trigger Property="IsMouseOver" Value="True"><Setter TargetName="Frame" Property="Background" Value="{DynamicResource Ui.AccentSoft}"/></Trigger>
                <Trigger Property="IsKeyboardFocused" Value="True"><Setter TargetName="Frame" Property="BorderBrush" Value="{DynamicResource Ui.Accent}"/></Trigger>
                <Trigger Property="IsPressed" Value="True"><Setter TargetName="Frame" Property="Background" Value="{DynamicResource Ui.AccentSoft}"/></Trigger>
                <Trigger Property="IsEnabled" Value="False"><Setter Property="Opacity" Value="0.45"/></Trigger>
              </ControlTemplate.Triggers>
            </ControlTemplate></Setter.Value></Setter>
          </Style>
          <Style TargetType="TextBox">
            <Setter Property="Foreground" Value="{DynamicResource Ui.Text}"/><Setter Property="Background" Value="{DynamicResource Ui.Surface}"/>
            <Setter Property="BorderBrush" Value="{DynamicResource Ui.Border}"/><Setter Property="BorderThickness" Value="1"/>
            <Setter Property="Padding" Value="7,5"/><Setter Property="MinHeight" Value="30"/>
            <Setter Property="CaretBrush" Value="{DynamicResource Ui.Text}"/>
            <Setter Property="SelectionBrush" Value="{DynamicResource Ui.Accent}"/>
            <Setter Property="Template"><Setter.Value><ControlTemplate TargetType="TextBox">
              <Border x:Name="Frame" Background="{TemplateBinding Background}" BorderBrush="{TemplateBinding BorderBrush}" BorderThickness="{TemplateBinding BorderThickness}" CornerRadius="4" Padding="{TemplateBinding Padding}">
                <ScrollViewer x:Name="PART_ContentHost"/>
              </Border>
              <ControlTemplate.Triggers><Trigger Property="IsKeyboardFocusWithin" Value="True"><Setter TargetName="Frame" Property="BorderThickness" Value="2"/></Trigger></ControlTemplate.Triggers>
            </ControlTemplate></Setter.Value></Setter>
          </Style>
          <Style TargetType="ToggleButton">
            <Setter Property="Foreground" Value="{DynamicResource Ui.Text}"/><Setter Property="Background" Value="{DynamicResource Ui.Surface}"/><Setter Property="BorderBrush" Value="{DynamicResource Ui.Border}"/>
            <Setter Property="Template"><Setter.Value><ControlTemplate TargetType="ToggleButton"><Border x:Name="Frame" Background="{TemplateBinding Background}" BorderBrush="{TemplateBinding BorderBrush}" BorderThickness="1" CornerRadius="4" Padding="{TemplateBinding Padding}"><ContentPresenter HorizontalAlignment="Center" VerticalAlignment="Center"/></Border><ControlTemplate.Triggers><Trigger Property="IsChecked" Value="True"><Setter TargetName="Frame" Property="Background" Value="{DynamicResource Ui.AccentSoft}"/><Setter TargetName="Frame" Property="BorderBrush" Value="{DynamicResource Ui.Accent}"/></Trigger></ControlTemplate.Triggers></ControlTemplate></Setter.Value></Setter>
          </Style>
          <Style TargetType="ComboBox"><Setter Property="Foreground" Value="{DynamicResource Ui.Text}"/><Setter Property="Background" Value="{DynamicResource Ui.Surface}"/><Setter Property="BorderBrush" Value="{DynamicResource Ui.Border}"/><Setter Property="Padding" Value="6,5"/><Setter Property="MinHeight" Value="30"/>
            <Setter Property="Template"><Setter.Value><ControlTemplate TargetType="ComboBox">
              <Grid><Border Background="{TemplateBinding Background}" BorderBrush="{TemplateBinding BorderBrush}" BorderThickness="1" CornerRadius="4"/>
                <ToggleButton x:Name="DropDown" Focusable="False" IsChecked="{Binding IsDropDownOpen, RelativeSource={RelativeSource TemplatedParent}, Mode=TwoWay}" Background="Transparent" BorderBrush="Transparent" HorizontalContentAlignment="Right"><ToggleButton.Template><ControlTemplate TargetType="ToggleButton"><Border Background="Transparent"><Path Data="M0,0 L4,4 8,0" Stroke="{DynamicResource Ui.Text}" StrokeThickness="1.5" Width="8" Height="5" Margin="0,0,10,0" HorizontalAlignment="Right" VerticalAlignment="Center"/></Border></ControlTemplate></ToggleButton.Template></ToggleButton>
                <ContentPresenter x:Name="Selected" Content="{TemplateBinding SelectionBoxItem}" ContentTemplate="{TemplateBinding SelectionBoxItemTemplate}" Margin="7,5,28,5" VerticalAlignment="Center" IsHitTestVisible="False"/>
                <TextBox x:Name="PART_EditableTextBox" Visibility="Hidden" Background="Transparent" BorderThickness="0" Margin="2,0,26,0" MinHeight="0" VerticalContentAlignment="Center" IsReadOnly="{TemplateBinding IsReadOnly}"/>
                <Popup x:Name="PART_Popup" IsOpen="{TemplateBinding IsDropDownOpen}" Placement="Bottom" AllowsTransparency="True" Focusable="False"><Border Background="{DynamicResource Ui.Surface}" BorderBrush="{DynamicResource Ui.Border}" BorderThickness="1" MinWidth="{Binding ActualWidth, RelativeSource={RelativeSource TemplatedParent}}"><ScrollViewer MaxHeight="300"><ItemsPresenter KeyboardNavigation.DirectionalNavigation="Contained"/></ScrollViewer></Border></Popup>
              </Grid>
              <ControlTemplate.Triggers><Trigger Property="IsEditable" Value="True"><Setter TargetName="PART_EditableTextBox" Property="Visibility" Value="Visible"/><Setter TargetName="Selected" Property="Visibility" Value="Hidden"/><Setter TargetName="DropDown" Property="HorizontalAlignment" Value="Right"/><Setter TargetName="DropDown" Property="Width" Value="25"/></Trigger><Trigger Property="IsEnabled" Value="False"><Setter Property="Opacity" Value="0.5"/></Trigger></ControlTemplate.Triggers>
            </ControlTemplate></Setter.Value></Setter>
          </Style>
          <Style TargetType="ComboBoxItem"><Setter Property="Foreground" Value="{DynamicResource Ui.Text}"/><Setter Property="Background" Value="{DynamicResource Ui.Surface}"/></Style>
          <Style TargetType="ListBox"><Setter Property="Foreground" Value="{DynamicResource Ui.Text}"/><Setter Property="Background" Value="{DynamicResource Ui.Surface}"/><Setter Property="BorderBrush" Value="{DynamicResource Ui.Border}"/><Setter Property="BorderThickness" Value="0"/></Style>
          <Style TargetType="ListBoxItem"><Setter Property="Foreground" Value="{DynamicResource Ui.Text}"/><Setter Property="Padding" Value="4"/>
            <Setter Property="Template"><Setter.Value><ControlTemplate TargetType="ListBoxItem"><Border x:Name="Frame" CornerRadius="4" Background="Transparent" Padding="{TemplateBinding Padding}"><ContentPresenter HorizontalAlignment="{TemplateBinding HorizontalContentAlignment}"/></Border><ControlTemplate.Triggers><Trigger Property="IsSelected" Value="True"><Setter TargetName="Frame" Property="Background" Value="{DynamicResource Ui.AccentSoft}"/></Trigger><Trigger Property="IsMouseOver" Value="True"><Setter TargetName="Frame" Property="Background" Value="{DynamicResource Ui.Raised}"/></Trigger></ControlTemplate.Triggers></ControlTemplate></Setter.Value></Setter>
          </Style>
          <Style TargetType="Menu"><Setter Property="Background" Value="{DynamicResource Ui.Surface}"/><Setter Property="Foreground" Value="{DynamicResource Ui.Text}"/></Style>
          <Style TargetType="MenuItem"><Setter Property="Foreground" Value="{DynamicResource Ui.Text}"/><Setter Property="Background" Value="{DynamicResource Ui.Surface}"/></Style>
          <Style TargetType="ContextMenu"><Setter Property="Foreground" Value="{DynamicResource Ui.Text}"/><Setter Property="Background" Value="{DynamicResource Ui.Surface}"/></Style>
          <Style TargetType="CheckBox"><Setter Property="Foreground" Value="{DynamicResource Ui.Text}"/></Style>
          <Style TargetType="TabControl"><Setter Property="Background" Value="{DynamicResource Ui.Surface}"/><Setter Property="BorderBrush" Value="{DynamicResource Ui.Border}"/></Style>
          <Style TargetType="TabItem"><Setter Property="Foreground" Value="{DynamicResource Ui.Text}"/><Setter Property="Background" Value="{DynamicResource Ui.Surface}"/><Setter Property="Padding" Value="12,5"/>
            <Setter Property="Template"><Setter.Value><ControlTemplate TargetType="TabItem"><Border x:Name="Frame" Background="{TemplateBinding Background}" Padding="{TemplateBinding Padding}" BorderThickness="0,0,0,2" BorderBrush="Transparent"><ContentPresenter ContentSource="Header"/></Border><ControlTemplate.Triggers><Trigger Property="IsSelected" Value="True"><Setter TargetName="Frame" Property="Background" Value="{DynamicResource Ui.AccentSoft}"/><Setter TargetName="Frame" Property="BorderBrush" Value="{DynamicResource Ui.Accent}"/></Trigger></ControlTemplate.Triggers></ControlTemplate></Setter.Value></Setter>
          </Style>
          <Style TargetType="Expander"><Setter Property="Foreground" Value="{DynamicResource Ui.Text}"/><Setter Property="Margin" Value="0,12,0,0"/>
            <Setter Property="Template"><Setter.Value><ControlTemplate TargetType="Expander"><StackPanel><ToggleButton IsChecked="{Binding IsExpanded, RelativeSource={RelativeSource TemplatedParent}, Mode=TwoWay}" HorizontalContentAlignment="Stretch"><ToggleButton.Template><ControlTemplate TargetType="ToggleButton"><DockPanel Background="Transparent" Margin="0,3"><Path x:Name="Arrow" Data="M0,0 L4,4 0,8" Stroke="{DynamicResource Ui.Muted}" StrokeThickness="1.5" Width="8" Height="8" Margin="0,0,6,0" VerticalAlignment="Center"/><ContentPresenter Content="{Binding Header, RelativeSource={RelativeSource AncestorType=Expander}}"/></DockPanel><ControlTemplate.Triggers><Trigger Property="IsChecked" Value="True"><Setter TargetName="Arrow" Property="Data" Value="M0,0 L4,4 8,0"/></Trigger></ControlTemplate.Triggers></ControlTemplate></ToggleButton.Template></ToggleButton><ContentPresenter x:Name="Body" ContentSource="Content" Visibility="Collapsed"/></StackPanel><ControlTemplate.Triggers><Trigger Property="IsExpanded" Value="True"><Setter TargetName="Body" Property="Visibility" Value="Visible"/></Trigger></ControlTemplate.Triggers></ControlTemplate></Setter.Value></Setter>
          </Style>
          <Style TargetType="ToolTip"><Setter Property="Foreground" Value="{DynamicResource Ui.Text}"/><Setter Property="Background" Value="{DynamicResource Ui.Surface}"/></Style>
          <Style TargetType="DataGrid"><Setter Property="Foreground" Value="{DynamicResource Ui.Text}"/><Setter Property="Background" Value="{DynamicResource Ui.Surface}"/><Setter Property="RowBackground" Value="{DynamicResource Ui.Surface}"/><Setter Property="AlternatingRowBackground" Value="{DynamicResource Ui.Raised}"/><Setter Property="BorderBrush" Value="{DynamicResource Ui.Border}"/></Style>
          <Style TargetType="DataGridColumnHeader"><Setter Property="Foreground" Value="{DynamicResource Ui.Text}"/><Setter Property="Background" Value="{DynamicResource Ui.Raised}"/><Setter Property="Padding" Value="8"/></Style>
          <Style TargetType="DataGridCell"><Setter Property="Foreground" Value="{DynamicResource Ui.Text}"/><Setter Property="BorderThickness" Value="0"/></Style>
        </ResourceDictionary>
        """;
}
