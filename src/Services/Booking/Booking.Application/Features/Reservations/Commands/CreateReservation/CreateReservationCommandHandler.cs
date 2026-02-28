using Booking.Domain.Entities;
using Booking.Domain.Interfaces;
using Booking.Application.Interfaces;
using MediatR;

namespace Booking.Application.Features.Reservations.Commands.CreateReservation;

public class CreateReservationCommandHandler : IRequestHandler<CreateReservationCommand, Guid>
{
    private readonly IReservationRepository _reservationRepository;
    private readonly ISeatRepository _seatRepository;
    private readonly IDistributedLockService _lockService;
    private readonly IPaymentService _paymentService;

    public CreateReservationCommandHandler(
        IReservationRepository reservationRepository,
        ISeatRepository seatRepository,
        IDistributedLockService lockService,
        IPaymentService paymentService)
    {
        _reservationRepository = reservationRepository;
        _seatRepository = seatRepository;
        _lockService = lockService;
        _paymentService = paymentService;
    }

    public async Task<Guid> Handle(CreateReservationCommand request, CancellationToken cancellationToken)
    {
        // 1. Lock Distribuído (Redis) - Proteção contra Race Condition
        string lockKey = $"lock:seat:{request.SeatId}";
        string lockValue = Guid.NewGuid().ToString();
        
        bool lockAcquired = await _lockService.AcquireLockAsync(lockKey, lockValue, TimeSpan.FromSeconds(10));
        if (!lockAcquired) throw new Exception("Assento em disputa por outro usuario.");

        try
        {
            var seat = await _seatRepository.GetByIdAsync(request.SeatId);
            if (seat == null || seat.IsReserved) throw new Exception("Assento indisponivel.");

            // 2. Passo Inicial: Criar Reserva como PENDENTE
            seat.Reserve();
            var reservation = new Reservation(request.SeatId, request.UserId, TimeSpan.FromMinutes(10));
            
            await _seatRepository.UpdateAsync(seat);
            await _reservationRepository.AddAsync(reservation);

            // 3. Chamada entre Microserviços (Orquestração Saga Simples)
            // Aqui estamos conectando o Booking.API com o Payment.API via HTTP
            decimal amount = 150.00m; // Preço fixo para teste
            bool paymentSuccess = await _paymentService.ProcessPaymentAsync(reservation.Id, amount);

            if (paymentSuccess)
            {
                // 4. Sucesso: Confirmar Reserva
                reservation.Confirm();
                await _reservationRepository.UpdateAsync(reservation);
            }
            else
            {
                // 5. FALHA (AÇÃO COMPENSATÓRIA): Liberar assento e expirar reserva
                seat.Release();
                reservation.Expire();
                await _seatRepository.UpdateAsync(seat);
                await _reservationRepository.UpdateAsync(reservation);
                throw new Exception("Pagamento recusado. Assento liberado.");
            }

            return reservation.Id;
        }
        finally
        {
            await _lockService.ReleaseLockAsync(lockKey, lockValue);
        }
    }
}
