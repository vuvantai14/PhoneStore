namespace PhoneStore.Admin;

static class Program
{
    [STAThread]
    static void Main()
    {
        ApplicationConfiguration.Initialize();
        try
        {
            using var services = AdminServices.Create();
            using var context = new AdminApplicationContext(services);
            Application.Run(context);
        }
        catch (Exception)
        {
            MessageBox.Show("Không thể khởi động PhoneStore Admin. Vui lòng kiểm tra cấu hình ứng dụng.",
                "PhoneStore Admin", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }
}
