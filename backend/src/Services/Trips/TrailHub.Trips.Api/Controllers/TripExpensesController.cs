using Microsoft.AspNetCore.Mvc;
using TrailHub.BuildingBlocks.Web;
using TrailHub.Trips.Application.Interfaces.Service;
using TrailHub.Trips.Application.Requests;
using TrailHub.Trips.Application.Responses;

namespace TrailHub.Trips.Api.Controllers;

/// <summary>Trip expenses: log costs, split them, see who owes whom, and record settle-up payments.</summary>
/// <remarks>
/// Participants are the trip owner plus, for a group trip, the group's members. Anyone who can see the trip can add
/// expenses and record payments, in any trip status. Amounts have two decimals; balances and shares are in the trip
/// currency (<c>PUT /api/trips/{id}/currency</c>). An expense in another currency carries a rate (trip-currency units
/// per one unit of its currency); <c>GET /api/trips/exchange-rate</c> suggests one. Changes send <c>TripChanged</c>
/// over SignalR like other trip edits.
/// </remarks>
[Route("api/trips")]
public sealed class TripExpensesController(ITripExpenseService expenseService, IExchangeRateService exchangeRateService) : ApiController
{
    /// <summary>Expenses (newest date first), payments, everyone's balance, settle-up suggestions and the participants.</summary>
    [HttpGet("{id:guid}/expenses")]
    [ProducesResponseType<TripExpensesResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<TripExpensesResponse>> List(Guid id, CancellationToken ct) =>
        Ok(await expenseService.GetAsync(id, ct));

    /// <summary>Logs an expense, split equally among <c>participants</c> or by <c>customShares</c> (in the expense currency).</summary>
    /// <remarks>
    /// Equal splits give leftover cents one each to participants in user-id order. Custom amounts must add up to
    /// <c>amount</c> (<c>Trip.SplitMismatch</c>). <c>rate</c> is required when <c>currency</c> isn't the trip currency.
    /// </remarks>
    [HttpPost("{id:guid}/expenses")]
    [ProducesResponseType<ExpenseResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ExpenseResponse>> Add(Guid id, ExpenseRequest body, CancellationToken ct)
    {
        var expense = await expenseService.AddExpenseAsync(id, body, ct);
        return Created($"/api/trips/{id}/expenses", expense);
    }

    /// <summary>Replaces an expense. Whoever added it, whoever paid it, or the trip owner.</summary>
    [HttpPut("{id:guid}/expenses/{expenseId:guid}")]
    [ProducesResponseType<ExpenseResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ExpenseResponse>> Update(Guid id, Guid expenseId, ExpenseRequest body, CancellationToken ct) =>
        Ok(await expenseService.UpdateExpenseAsync(id, expenseId, body, ct));

    /// <summary>Deletes an expense. Whoever added it, whoever paid it, or the trip owner.</summary>
    [HttpDelete("{id:guid}/expenses/{expenseId:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(Guid id, Guid expenseId, CancellationToken ct)
    {
        await expenseService.DeleteExpenseAsync(id, expenseId, ct);
        return NoContent();
    }

    /// <summary>Records that <c>from</c> paid <c>to</c> an amount in the trip currency ("Mark paid"). It updates both balances.</summary>
    [HttpPost("{id:guid}/payments")]
    [ProducesResponseType<PaymentResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<PaymentResponse>> RecordPayment(Guid id, PaymentRequest body, CancellationToken ct)
    {
        var payment = await expenseService.RecordPaymentAsync(id, body, ct);
        return Created($"/api/trips/{id}/expenses", payment);
    }

    /// <summary>Deletes a payment. Whoever recorded it, the two people in it, or the trip owner.</summary>
    [HttpDelete("{id:guid}/payments/{paymentId:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeletePayment(Guid id, Guid paymentId, CancellationToken ct)
    {
        await expenseService.DeletePaymentAsync(id, paymentId, ct);
        return NoContent();
    }

    /// <summary>A suggested rate: units of <c>to</c> per one <c>from</c> on <c>date</c> (ECB reference rates).</summary>
    /// <remarks>The response's date is the rate's own (the previous business day on weekends; today's rate for future dates).</remarks>
    /// <param name="from">The expense currency, e.g. <c>CHF</c>.</param>
    /// <param name="to">The trip currency, e.g. <c>EUR</c>.</param>
    /// <param name="date">The expense date (YYYY-MM-DD); today when left out.</param>
    [HttpGet("exchange-rate")]
    [ProducesResponseType<ExchangeRateResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status503ServiceUnavailable)]
    public async Task<ActionResult<ExchangeRateResponse>> ExchangeRate([FromQuery] string? from, [FromQuery] string? to, [FromQuery] DateOnly? date, CancellationToken ct) =>
        Ok(await exchangeRateService.GetRateAsync(from, to, date, ct));

    /// <summary>Currencies expenses can be entered in.</summary>
    [HttpGet("currencies")]
    [ProducesResponseType<IReadOnlyList<CurrencyResponse>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<CurrencyResponse>>> Currencies(CancellationToken ct) =>
        Ok(await exchangeRateService.GetCurrenciesAsync(ct));
}
