import { HttpClient } from '@angular/common/http';
import { Component } from '@angular/core';
import { FormsModule, NgForm } from '@angular/forms';

@Component({
  selector: 'app-root',
  imports: [FormsModule],
  templateUrl: './app.html',
})
export class App {
  protected readonly order = {
    symbol: '',
    side: '',
    quantity: null as number | null,
    price: null as number | null,
  };
  protected execType = '';
  protected errorMessage = '';
  protected submitting = false;

  constructor(private readonly http: HttpClient) {}

  protected submit(form: NgForm): void {
    const { symbol, side, quantity, price } = this.order;
    const priceInCents = (price ?? 0) * 100;
    const validOrder =
      ['PETR4', 'VALE3', 'VIIA4'].includes(symbol) &&
      ['Compra', 'Venda'].includes(side) &&
      Number.isInteger(quantity) &&
      quantity !== null &&
      quantity > 0 &&
      quantity < 100_000 &&
      price !== null &&
      price > 0 &&
      price < 1_000 &&
      Math.abs(priceInCents - Math.round(priceInCents)) < 0.000001;

    if (form.invalid || !validOrder || quantity === null || price === null) {
      form.control.markAllAsTouched();
      this.errorMessage = 'Informe todos os campos com valores válidos.';
      this.execType = '';
      return;
    }

    this.errorMessage = '';
    this.execType = '';
    this.submitting = true;

    this.http
      .post<{ execType: string }>('http://localhost:5000/orders', {
        symbol,
        side,
        quantity,
        price,
      })
      .subscribe({
        next: (response) => {
          this.execType = response.execType;
          this.submitting = false;
        },
        error: () => {
          this.errorMessage = 'Não foi possível enviar a ordem.';
          this.submitting = false;
        },
      });
  }
}
