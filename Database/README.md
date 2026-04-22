# ChillTour Database Design

`schema.sql` la bo schema SQL Server day du cho he thong dat tour.

## Pham vi chuc nang

- Dang ky, dang nhap, vai tro, session, xac thuc email, quen mat khau
- Quan ly danh muc, diem den, tour, media, tag
- Lich khoi hanh, lich trinh tung ngay, khach san, loai phong, phuong tien
- Dat tour, hanh khach, thanh toan, hoan tien, lich su trang thai
- Khuyen mai, coupon, ap dung khuyen mai theo tour
- Danh gia sao, binh luan, anh review, like review
- Wishlist, bai viet cam nang, thong bao

## Nhom bang chinh

- `auth.*`: tai khoan va phan quyen
- `catalog.*`: du lieu tour va san pham
- `booking.*`: dat cho, thanh toan, khuyen mai
- `content.*`: review, wishlist, bai viet
- `integration.*`: thong bao

## Quy uoc trang thai de implement

- `auth.Users.Status`: `0=Deleted`, `1=Active`, `2=Locked`, `3=Pending`
- `catalog.Tours.ApprovalStatus`: `0=Draft`, `1=Pending`, `2=Approved`, `3=Rejected`
- `catalog.TourSchedules.Status`: `0=Closed`, `1=Open`, `2=SoldOut`, `3=Completed`, `4=Cancelled`
- `booking.Bookings.BookingStatus`: `0=Pending`, `1=Confirmed`, `2=Paid`, `3=Completed`, `4=Cancelled`, `5=Refunded`
- `booking.Bookings.PaymentStatus`: `0=Unpaid`, `1=Partial`, `2=Paid`, `3=Refunded`, `4=Failed`
- `booking.Payments.PaymentMethod`: `1=COD`, `2=BankTransfer`, `3=CreditCard`, `4=EWallet`, `5=VNPayOrGateway`
- `content.Reviews.ModerationStatus`: `0=Pending`, `1=Approved`, `2=Rejected`, `3=Hidden`

## Goi y phat trien tiep theo

- Dung EF Core map 1-1 theo schema nay
- Tach `schema.sql` thanh migration nho hon theo tung module
- Them stored procedure cho quy trinh giu cho, xac nhan booking, giai phong ghe
- Them service layer de cap nhat `AvailableSeats` va `ReservedSeats`
- Them bang audit log neu ban can theo doi lich su thay doi du lieu
