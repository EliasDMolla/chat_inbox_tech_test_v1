import { ComponentFixture, TestBed } from '@angular/core/testing';
import { MessageComposer } from './message-composer';

describe('MessageComposer', () => {
  let fixture: ComponentFixture<MessageComposer>;
  beforeEach(async () => {
    await TestBed.configureTestingModule({ imports: [MessageComposer] }).compileComponents();
    fixture = TestBed.createComponent(MessageComposer);
  });

  it('does not send whitespace-only text', () => {
    fixture.componentRef.setInput('value', '   ');
    fixture.detectChanges();
    const emit = vi.spyOn(fixture.componentInstance.sendRequested, 'emit');
    (fixture.nativeElement.querySelector('button') as HTMLButtonElement).click();
    expect(emit).not.toHaveBeenCalled();
  });

  it('disables sending while a request is pending', () => {
    fixture.componentRef.setInput('value', 'Hola');
    fixture.componentRef.setInput('sending', true);
    fixture.detectChanges();
    expect((fixture.nativeElement.querySelector('button') as HTMLButtonElement).disabled).toBe(
      true,
    );
  });
});
