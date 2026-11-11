using System.Diagnostics;

namespace BarDock.Setup;

internal sealed class SetupForm : Form
{
    private readonly CheckBox _desktop = new();
    private readonly CheckBox _startup = new();
    private readonly CheckBox _launch = new();
    private readonly Label _status = new();
    private readonly Button _install = new();

    public SetupForm()
    {
        Text = $"BarDock {Installer.Version}";
        ClientSize = new Size(480, 330);
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        StartPosition = FormStartPosition.CenterScreen;
        Font = new Font("Segoe UI", 9F);

        try
        {
            Icon = System.Drawing.Icon.ExtractAssociatedIcon(Application.ExecutablePath);
        }
        catch
        {
            Icon = SystemIcons.Application;
        }

        var title = new Label
        {
            Text = "BarDock",
            Font = new Font("Segoe UI", 20F, FontStyle.Bold),
            AutoSize = true,
            Location = new Point(22, 16),
        };

        var subtitle = new Label
        {
            Text = "Barra superior tipo macOS para Windows 11.",
            AutoSize = true,
            Location = new Point(25, 60),
        };

        var where = new Label
        {
            Text = "Se instalara para este usuario en:\n" + Installer.InstallDir,
            AutoSize = false,
            Size = new Size(430, 40),
            Location = new Point(25, 88),
            ForeColor = Color.DimGray,
        };

        _desktop.Text = "Crear acceso directo en el escritorio";
        _desktop.Checked = true;
        _desktop.AutoSize = true;
        _desktop.Location = new Point(25, 138);

        _startup.Text = "Iniciar con Windows";
        _startup.AutoSize = true;
        _startup.Location = new Point(25, 166);

        _launch.Text = "Abrir BarDock al terminar";
        _launch.Checked = true;
        _launch.AutoSize = true;
        _launch.Location = new Point(25, 194);

        _status.Text = "";
        _status.AutoSize = false;
        _status.Size = new Size(430, 22);
        _status.Location = new Point(25, 228);
        _status.ForeColor = Color.DimGray;

        _install.Text = "Instalar";
        _install.Size = new Size(120, 34);
        _install.Location = new Point(226, 268);
        _install.Click += (_, _) => RunInstall();

        var close = new Button
        {
            Text = "Cerrar",
            Size = new Size(100, 34),
            Location = new Point(356, 268),
        };
        close.Click += (_, _) => Close();

        AcceptButton = _install;
        CancelButton = close;

        Controls.AddRange(new Control[]
        {
            title, subtitle, where, _desktop, _startup, _launch, _status, _install, close,
        });
    }

    private void RunInstall()
    {
        _install.Enabled = false;
        _status.Text = "Instalando...";
        Application.DoEvents();

        try
        {
            Installer.Install(_desktop.Checked, _startup.Checked, _launch.Checked);
            _status.Text = "Instalado correctamente.";
            MessageBox.Show(
                this,
                "BarDock se ha instalado correctamente.",
                "BarDock",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
            Close();
        }
        catch (Exception ex)
        {
            _status.Text = "No se pudo instalar.";
            MessageBox.Show(this, ex.Message, "BarDock", MessageBoxButtons.OK, MessageBoxIcon.Error);
            _install.Enabled = true;
        }
    }
}
