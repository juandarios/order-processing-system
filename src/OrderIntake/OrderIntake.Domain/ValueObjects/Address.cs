using OrderIntake.Domain.Exceptions;

namespace OrderIntake.Domain.ValueObjects;

/// <summary>
/// Represents a shipping address. Immutable value object.
/// </summary>
/// <param name="Street">Street address line.</param>
/// <param name="City">City name.</param>
/// <param name="Country">ISO 3166-1 alpha-2 country code (e.g., "US").</param>
/// <param name="ZipCode">Postal/ZIP code.</param>
public record Address(string Street, string City, string Country, string ZipCode)
{
    /// <summary>Gets the street address.</summary>
    public string Street { get; } = !string.IsNullOrWhiteSpace(Street)
        ? Street : throw new DomainException("Street cannot be empty.");

    /// <summary>Gets the city.</summary>
    public string City { get; } = !string.IsNullOrWhiteSpace(City)
        ? City : throw new DomainException("City cannot be empty.");

    /// <summary>Gets the country code.</summary>
    public string Country { get; } = !string.IsNullOrWhiteSpace(Country)
        ? Country.ToUpperInvariant() : throw new DomainException("Country cannot be empty.");

    /// <summary>Gets the ZIP/postal code.</summary>
    public string ZipCode { get; } = !string.IsNullOrWhiteSpace(ZipCode)
        ? ZipCode : throw new DomainException("ZipCode cannot be empty.");
}
