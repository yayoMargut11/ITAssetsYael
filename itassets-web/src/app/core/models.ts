export interface LoginResponse {
  token: string;
  expiresAtUtc: string;
  username: string;
  role: string;
}

export interface PagedResult<T> {
  items: T[];
  page: number;
  pageSize: number;
  totalCount: number;
  totalPages: number;
}

export interface Asset {
  id: number;
  assetCode: string;
  serialNumber: string | null;
  category: string;
  brand: string;
  model: string;
  ownershipType: string;
  supplierId: number | null;
  supplierName: string | null;
  status: string;
  currentLocation: string | null;
  purchaseDate: string | null;
  rentalEndDate: string | null;
  createdAt: string;
  updatedAt: string;
  rowVer?: string | null;
  assignedEmployeeId: number | null;
  assignedEmployeeName: string | null;
}

export interface AssetQuery {
  search?: string;
  status?: string;
  category?: string;
  page: number;
  pageSize: number;
}

export interface CreateAssetRequest {
  assetCode: string;
  serialNumber: string | null;
  category: string;
  brand: string;
  model: string;
  ownershipType: string;
  supplierId: number | null;
  currentLocation: string | null;
  purchaseDate: string | null;
  rentalEndDate: string | null;
}

export interface AssetMovement {
  id: number;
  assetId: number;
  movementType: string;
  previousValue: string | null;
  newValue: string | null;
  employeeId: number | null;
  employeeName: string | null;
  performedByUserId: number;
  performedBy: string;
  performedAt: string;
  notes: string | null;
}

export interface Employee {
  id: number;
  employeeNumber: string;
  fullName: string;
  email: string;
  isActive: boolean;
}

export interface Supplier {
  id: number;
  name: string;
  email: string | null;
  phone: string | null;
  isActive: boolean;
  services: string[];
}

export const ASSET_STATUSES = ['Disponible', 'Asignado', 'Mantenimiento', 'Retirado'] as const;
export const CATEGORIES = ['Laptop', 'Monitor', 'Impresora', 'Celular', 'Periférico'] as const;
export const OWNERSHIP_TYPES = ['Propio', 'Arrendado'] as const;
