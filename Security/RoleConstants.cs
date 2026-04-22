namespace ChillTour.Security;

public static class RoleConstants
{
    public const string Admin = "Admin";
    public const string Director = "Director";
    public const string Manager = "Manager";
    public const string Accountant = "Accountant";
    public const string Employee = "Employee";
    public const string Customer = "Customer";

    public static readonly string[] All = [Admin, Director, Manager, Accountant, Employee, Customer];
    public static readonly string[] BackOffice = [Admin, Director, Manager, Accountant, Employee];
    public static readonly string[] ManageUsers = [Admin];
    public static readonly string[] ViewReports = [Admin, Director, Manager, Accountant, Employee];
    public static readonly string[] ManageTours = [Admin, Manager];
    public static readonly string[] ManageBookings = [Employee];
    public static readonly string[] ViewBookings = [Admin, Director, Manager, Accountant, Employee];
    public static readonly string[] ManageFinance = [Accountant];
    public static readonly string[] ManagePromotions = [Admin, Manager];
    public static readonly string[] ManageContent = [Admin, Manager, Employee];

    public static string Join(params string[] roles) => string.Join(",", roles);
    public static string Join(IEnumerable<string> roles) => string.Join(",", roles);

    public static string DisplayName(string roleCode)
    {
        return roleCode.Trim().ToUpperInvariant() switch
        {
            "ADMIN" => "Admin",
            "DIRECTOR" => "Director",
            "MANAGER" => "Manager",
            "ACCOUNTANT" => "Accountant",
            "EMPLOYEE" or "STAFF" => "Employee/Staff",
            "CUSTOMER" => "Customer",
            _ => roleCode
        };
    }
}

public static class PermissionConstants
{
    public const string AccessAdmin = "Permission.AccessAdmin";
    public const string ManageUsers = "Permission.ManageUsers";
    public const string ViewReports = "Permission.ViewReports";
    public const string ManageTours = "Permission.ManageTours";
    public const string ViewBookings = "Permission.ViewBookings";
    public const string ManageBookings = "Permission.ManageBookings";
    public const string ManageFinance = "Permission.ManageFinance";
    public const string ManagePromotions = "Permission.ManagePromotions";
    public const string ManageContent = "Permission.ManageContent";
}
