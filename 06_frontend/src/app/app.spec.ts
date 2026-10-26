import { TestBed } from '@angular/core/testing';
import { provideRouter, RouterOutlet } from '@angular/router';
import { By } from '@angular/platform-browser';

import { App } from './app';

describe('App', () => {
  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [App],
      providers: [provideRouter([])],
    }).compileComponents();
  });

  it('se crea', () => {
    expect(TestBed.createComponent(App).componentInstance).toBeTruthy();
  });

  it('solo pinta las rutas: cada vista trae su propio encabezado', () => {
    const fixture = TestBed.createComponent(App);

    expect(fixture.debugElement.query(By.directive(RouterOutlet))).not.toBeNull();
  });
});
