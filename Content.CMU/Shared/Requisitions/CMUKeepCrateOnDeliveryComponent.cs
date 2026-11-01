namespace Content.Shared.CMU14.Requisitions;

/// <summary>
/// Marks an ASRS product crate that must be delivered as a crate instead of
/// having its StorageFill contents expanded into loose shipment items. The
/// contents stay inside and are released when the crate is opened.
/// </summary>
[RegisterComponent]
public sealed partial class CMUKeepCrateOnDeliveryComponent : Component;
