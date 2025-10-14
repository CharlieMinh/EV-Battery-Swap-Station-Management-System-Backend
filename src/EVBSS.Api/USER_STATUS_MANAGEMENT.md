# User Status Management - API Documentation

## Overview
Tính năng quản lý trạng thái tài khoản người dùng với 2 trạng thái:
- **Active (0)**: Hoạt động - Mặc định cho tất cả user mới
- **Locked (1)**: Bị khóa - Không thể đăng nhập

## Database Changes

### Migration: `AddUserStatus`
```sql
ALTER TABLE [Users] ADD [Status] int NOT NULL DEFAULT 0;
```

### User Model
```csharp
public enum UserStatus 
{ 
    Active = 0,      // Hoạt động
    Locked = 1       // Bị khóa
}

public class User
{
    // ... existing fields
    public UserStatus Status { get; set; } = UserStatus.Active; // Default: Active
}
```

## API Changes

### 1. All User List Endpoints
Tất cả các endpoint list users đều trả về thêm field `status`:

#### GET `/api/v1/users`
```json
{
  "data": [
    {
      "id": "...",
      "email": "user@example.com",
      "name": "User Name",
      "phoneNumber": "0901234567",
      "role": "Driver",
      "status": "Active",  // ← NEW
      "createdAt": "2024-09-15T10:30:00Z",
      "lastLogin": "2025-10-14T08:15:00Z"
    }
  ]
}
```

#### GET `/api/v1/users/customers`
```json
{
  "data": [
    {
      "id": "...",
      "email": "customer@example.com",
      "name": "Customer Name",
      "phoneNumber": "0901234567",
      "status": "Active",  // ← NEW
      "createdAt": "2024-09-15T10:30:00Z",
      "lastLogin": "2025-10-14T08:15:00Z",
      "totalReservations": 10,
      "completedReservations": 8
    }
  ]
}
```

#### GET `/api/v1/users/staff`
```json
{
  "data": [
    {
      "id": "...",
      "email": "staff@example.com",
      "name": "Staff Name",
      "phoneNumber": "0901234567",
      "role": "Staff",
      "status": "Active",  // ← NEW
      "createdAt": "2024-09-15T10:30:00Z",
      "lastLogin": "2025-10-14T08:15:00Z"
    }
  ]
}
```

### 2. Detail Endpoints

#### GET `/api/v1/users/{id}`
```json
{
  "id": "...",
  "email": "user@example.com",
  "name": "User Name",
  "phoneNumber": "0901234567",
  "role": "Driver",
  "status": "Active",  // ← NEW
  "createdAt": "2024-09-15T10:30:00Z",
  "lastLogin": "2025-10-14T08:15:00Z",
  "totalReservations": 10,
  "completedReservations": 8,
  "cancelledReservations": 1,
  "totalVehicles": 2
}
```

#### GET `/api/v1/users/staff/{id}`
```json
{
  "id": "...",
  "email": "staff@example.com",
  "name": "Staff Name",
  "phoneNumber": "0901234567",
  "role": "Staff",
  "status": "Active",  // ← NEW
  "createdAt": "2024-09-15T10:30:00Z",
  "lastLogin": "2025-10-14T08:15:00Z",
  "totalReservationsVerified": 156,
  "totalSwapTransactions": 234,
  "recentReservationsVerified": 42,
  "recentSwapTransactions": 67
}
```

### 3. Update User Endpoint

#### PUT `/api/v1/users/{id}`

**Request Body:**
```json
{
  "name": "New Name",
  "phoneNumber": "0987654321",
  "role": 1,         // Optional, Admin only
  "status": 1        // ← NEW: Optional, Admin only (0 = Active, 1 = Locked)
}
```

**Authorization Rules:**
| Role | Can Update | Can Change Role | Can Change Status |
|------|-----------|----------------|------------------|
| Driver | Own profile only (Name, Phone) | ❌ No | ❌ No |
| Staff | Driver profiles (Name, Phone) | ❌ No | ❌ No |
| Admin | Any user (All fields) | ✅ Yes | ✅ Yes |

**Response:**
```json
{
  "id": "...",
  "email": "user@example.com",
  "name": "New Name",
  "phoneNumber": "0987654321",
  "role": "Staff",
  "status": "Locked",  // ← Updated
  "createdAt": "2024-09-15T10:30:00Z",
  "lastLogin": "2025-10-14T08:15:00Z"
}
```

**Error Responses:**

If Staff tries to change status:
```json
{
  "error": "Staff members are not allowed to change user status"
}
```

If Driver tries to change status:
```json
{
  "error": "You are not allowed to change your role"
}
```

## Authentication Changes

### Login Endpoints

Both login endpoints now check user status:

#### POST `/api/v1/auth/login`
#### POST `/api/v1/auth/google-login`

**Error Response for Locked Account:**
```json
{
  "error": {
    "code": "ACCOUNT_LOCKED",
    "message": "Your account has been locked. Please contact administrator."
  }
}
```

**Status Code:** `401 Unauthorized`

## Frontend Integration

### TypeScript Types
```typescript
enum UserStatus {
  Active = 0,
  Locked = 1
}

interface UserResponse {
  id: string;
  email: string;
  name: string | null;
  phoneNumber: string | null;
  role: string;
  status: string;  // "Active" or "Locked"
  createdAt: string;
  lastLogin: string | null;
}

interface UpdateUserRequest {
  name?: string;
  phoneNumber?: string;
  role?: number;
  status?: number;  // 0 = Active, 1 = Locked (Admin only)
}
```

### Lock/Unlock User Example
```typescript
// Admin function to lock a user
const lockUser = async (userId: string) => {
  try {
    const response = await axios.put(
      `/api/v1/users/${userId}`,
      { status: 1 },  // 1 = Locked
      {
        headers: { Authorization: `Bearer ${adminToken}` }
      }
    );
    console.log('User locked:', response.data);
  } catch (error) {
    console.error('Error locking user:', error);
  }
};

// Admin function to unlock a user
const unlockUser = async (userId: string) => {
  try {
    const response = await axios.put(
      `/api/v1/users/${userId}`,
      { status: 0 },  // 0 = Active
      {
        headers: { Authorization: `Bearer ${adminToken}` }
      }
    );
    console.log('User unlocked:', response.data);
  } catch (error) {
    console.error('Error unlocking user:', error);
  }
};
```

### React Component Example
```tsx
import { useState } from 'react';
import { Button, Badge } from '@/components/ui';

interface UserRowProps {
  user: UserResponse;
  onStatusChange: (userId: string, newStatus: number) => Promise<void>;
}

const UserRow: React.FC<UserRowProps> = ({ user, onStatusChange }) => {
  const [loading, setLoading] = useState(false);

  const toggleStatus = async () => {
    setLoading(true);
    try {
      const newStatus = user.status === 'Active' ? 1 : 0;
      await onStatusChange(user.id, newStatus);
    } finally {
      setLoading(false);
    }
  };

  return (
    <tr>
      <td>{user.name}</td>
      <td>{user.email}</td>
      <td>{user.role}</td>
      <td>
        <Badge variant={user.status === 'Active' ? 'success' : 'danger'}>
          {user.status === 'Active' ? 'Hoạt động' : 'Bị khóa'}
        </Badge>
      </td>
      <td>
        <Button 
          onClick={toggleStatus} 
          disabled={loading}
          variant={user.status === 'Active' ? 'destructive' : 'success'}
        >
          {loading ? 'Đang xử lý...' : 
           user.status === 'Active' ? 'Khóa tài khoản' : 'Mở khóa'}
        </Button>
      </td>
    </tr>
  );
};
```

### Handle Login Error
```typescript
const handleLogin = async (email: string, password: string) => {
  try {
    const response = await axios.post('/api/v1/auth/login', {
      email,
      password
    });
    // Login successful
    localStorage.setItem('token', response.data.token);
  } catch (error) {
    if (error.response?.data?.error?.code === 'ACCOUNT_LOCKED') {
      alert('Tài khoản của bạn đã bị khóa. Vui lòng liên hệ quản trị viên.');
    } else {
      alert('Email hoặc mật khẩu không đúng.');
    }
  }
};
```

## Use Cases

### 1. Admin locks a problematic user
```http
PUT /api/v1/users/{userId}
Authorization: Bearer {admin_token}
Content-Type: application/json

{
  "status": 1
}
```

### 2. User tries to login with locked account
```http
POST /api/v1/auth/login
Content-Type: application/json

{
  "email": "locked-user@example.com",
  "password": "password123"
}
```

**Response:**
```json
{
  "error": {
    "code": "ACCOUNT_LOCKED",
    "message": "Your account has been locked. Please contact administrator."
  }
}
```

### 3. Admin unlocks the user
```http
PUT /api/v1/users/{userId}
Authorization: Bearer {admin_token}
Content-Type: application/json

{
  "status": 0
}
```

### 4. User can login again
User can now login successfully.

## Notes

- **Default Status**: All new users are created with `Status = Active` (0)
- **Existing Users**: After migration, all existing users automatically have `Status = Active` (0)
- **Only Admin**: Only users with Admin role can change user status
- **Login Prevention**: Locked users cannot login via both normal and Google login
- **No Self-Lock**: Admin should be careful not to lock themselves
- **Audit Trail**: Consider logging status changes for audit purposes (future enhancement)

## Migration Checklist

- [x] Database migration applied
- [x] Model updated
- [x] DTOs updated (Request & Response)
- [x] Controller endpoints updated
- [x] Authentication logic updated
- [x] Authorization checks implemented
- [x] Documentation created
- [ ] Frontend updated
- [ ] Testing completed
- [ ] Deployed to production
