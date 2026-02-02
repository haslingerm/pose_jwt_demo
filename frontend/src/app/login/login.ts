import {
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
import { AuthService } from "../../core/auth/auth-service";
import { UserService } from "../../core/user/user-service";
import { MatSnackBar } from "@angular/material/snack-bar";
import { form, required, submit, FormField } from "@angular/forms/signals";

@Component({
    selector: 'app-login',
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
        FormField
    ],
    templateUrl: './login.html',
    styleUrl: './login.scss'
})
export class Login implements OnInit {
  private static readonly DEFAULT_LOGIN_FORM_MODEL: LoginFormModel = {
    username: '',
    password: ''
  };
  private readonly authService = inject(AuthService);
  private readonly userService = inject(UserService);
  private readonly snackbarService = inject(MatSnackBar);

  public readonly userName: WritableSignal<string | null> = signal(null);
  public readonly isLoggedIn: Signal<boolean> = computed(() => this.userName() !== null);
  private readonly loginFormModel: WritableSignal<LoginFormModel> = signal(Login.DEFAULT_LOGIN_FORM_MODEL);
  protected readonly loginForm = form(this.loginFormModel, path => {
    required(path.username, {message: 'Username is required'});
    required(path.password, {message: 'Password is required'});
  });

  public async ngOnInit(): Promise<void> {
    await this.doLoginCheck();
  }

  public async submit(): Promise<void> {
    await submit(this.loginForm, async form => {
      const data = form().value();
      const result = await this.authService.login(data.username, data.password);
      if (result) {
        await this.doLoginCheck();
      } else {
        this.snackbarService.open("Login failed!", "Close", {duration: 2000});
      }
    });
  }

  public async logout(): Promise<void> {
    await this.authService.logout();
    this.userName.set(null);
    this.loginFormModel.set(Login.DEFAULT_LOGIN_FORM_MODEL);
    this.loginForm().reset();
  }

  private async doLoginCheck(): Promise<void> {
    const user = await this.userService.getUser();
    this.userName.set(user?.username ?? null);
  }
}

type LoginFormModel = {
  username: string;
  password: string;
}
