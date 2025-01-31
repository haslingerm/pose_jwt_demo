import {
  ChangeDetectionStrategy,
  Component,
  computed,
  inject,
  OnInit,
  signal,
  Signal,
  WritableSignal
} from "@angular/core";
import { MatCard, MatCardActions, MatCardContent, MatCardHeader, MatCardTitle } from "@angular/material/card";
import { MatError, MatFormField } from "@angular/material/form-field";
import { MatButton } from "@angular/material/button";
import { MatInput } from "@angular/material/input";
import { FormBuilder, ReactiveFormsModule, Validators } from "@angular/forms";
import { AuthService } from "../../core/auth/auth.service";
import { UserService } from "../../core/user/user.service";
import { MatSnackBar } from "@angular/material/snack-bar";

@Component({
    selector: 'app-login',
    changeDetection: ChangeDetectionStrategy.OnPush,
    imports: [
        MatCard,
        MatCardHeader,
        MatCardContent,
        MatCardTitle,
        MatError,
        MatFormField,
        MatCardActions,
        MatButton,
        MatInput,
        ReactiveFormsModule
    ],
    templateUrl: './login.component.html',
    styleUrl: './login.component.scss'
})
export class LoginComponent implements OnInit {
  private readonly fb = inject(FormBuilder);
  private readonly authService = inject(AuthService);
  private readonly userService = inject(UserService);
  private readonly snackbarService = inject(MatSnackBar);

  public readonly userName: WritableSignal<string | null> = signal(null);
  public readonly isLoggedIn: Signal<boolean> = computed(() => this.userName() !== null);
  public loginForm = this.fb.group({
    username: ['', Validators.required],
    password: ['', Validators.required]
  });

  public async ngOnInit(): Promise<void> {
    await this.doLoginCheck();
  }

  public async submit(): Promise<void> {
    const data: ILoginRequest = this.loginForm.value as ILoginRequest;
    const result = await this.authService.login(data.username, data.password);
    if (result) {
      await this.doLoginCheck();
    } else {
      this.snackbarService.open("Login failed!", "Close", {duration: 2000});
    }
  }

  public async logout(): Promise<void> {
    await this.authService.logout();
    this.userName.set(null);
    this.loginForm.reset();
  }

  private async doLoginCheck(): Promise<void> {
    const user = await this.userService.getUser();
    this.userName.set(user?.username ?? null);
  }
}

interface ILoginRequest {
  username: string;
  password: string;
}
